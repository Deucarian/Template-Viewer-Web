import { DeucarianCommandHost } from "./deucarian-command-host.js";

const RESPONSE_TIMEOUT_MILLISECONDS = 30000;

export function materializePayload(value, revision) {
  if (value === "$revision") return revision;
  if (value === "$stale_revision") return Math.max(0, revision - 2);
  if (Array.isArray(value)) {
    return value.map(item => materializePayload(item, revision));
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, item]) =>
      [key, materializePayload(item, revision)]));
  }
  return value;
}

export function createScenarioEnvelope(scenario, sequence, revision) {
  return {
    protocol_version: 1,
    command_id: `harness-${sequence}-${scenario.id}`,
    command: scenario.command,
    payload: materializePayload(scenario.payload || {}, revision),
    metadata: { source: "local-harness" }
  };
}

export async function startHarness(options = {}) {
  const windowRef = options.windowRef || window;
  const documentRef = options.documentRef || document;
  const fetchImpl = options.fetchImpl || fetch;
  const output = documentRef.querySelector("#events");
  const iframe = documentRef.querySelector("#viewer");
  const actions = documentRef.querySelector("#actions");
  const runButton = documentRef.querySelector("#run-automatic");
  const reloadButton = documentRef.querySelector("#reload-viewer");
  const connection = documentRef.querySelector("#connection-state");
  const query = new URLSearchParams(windowRef.location.search || "");
  const catalogUrl = query.get("catalog") || "./commands.generated.json";
  const catalog = await readJson(fetchImpl, catalogUrl);
  validateCatalog(catalog);
  const transportId = resolveCatalogTransportId(catalog);

  let configuredViewer = query.get("viewer");
  if (!configuredViewer) {
    try {
      const configuration = await readJson(
        fetchImpl,
        "./harness-config.generated.json");
      configuredViewer = configuration.viewer_path;
    } catch (_) {
      configuredViewer = "./mock-viewer.html";
    }
  }

  const viewerUrl = new URL(configuredViewer, windowRef.location.href);
  if (viewerUrl.origin !== windowRef.location.origin) {
    throw new Error("The local viewer must use the harness origin.");
  }
  iframe.src = viewerUrl.href;

  let revision = 0;
  let sequence = 0;
  let ready = false;
  let disposed = false;
  const pending = new Map();
  const readyWaiters = new Set();
  const host = new DeucarianCommandHost({
    hostWindow: windowRef,
    iframe,
    targetOrigin: windowRef.location.origin,
    transportId
  });

  function write(value) {
    output.textContent = `${new Date().toISOString()} ${value}\n${output.textContent}`;
  }

  function setReady(value) {
    ready = value;
    connection.dataset.ready = value ? "true" : "false";
    connection.textContent = value ? "Transport ready" : "Connecting";
    if (value) {
      for (const resolve of readyWaiters) resolve();
      readyWaiters.clear();
    }
  }

  function waitUntilReady() {
    if (ready) return Promise.resolve();
    return new Promise(resolve => readyWaiters.add(resolve));
  }

  function acceptResponse(value) {
    write("response " + JSON.stringify(value));
    const response = value?.message || {};
    const waiting = pending.get(response.command_id);
    if (!waiting) return;
    pending.delete(response.command_id);
    clearTimeout(waiting.timeout);
    waiting.resolve(response);
  }

  host.on("deucarian-command-ready", () => {
    setReady(true);
    write("transport ready");
  });
  host.on("deucarian-command-unavailable", value => {
    setReady(false);
    write("transport unavailable " + JSON.stringify(value));
  });
  host.on("deucarian-command-response", acceptResponse);
  host.on("deucarian-command-event", value =>
    write("event " + JSON.stringify(value)));
  host.on("deucarian-command-error", value =>
    write("transport error " + JSON.stringify(value)));
  host.start();

  async function sendScenario(scenario, waitForResponse = false) {
    if (disposed) throw new Error("The harness is disposed.");
    revision += 1;
    sequence += 1;
    const envelope = createScenarioEnvelope(scenario, sequence, revision);
    write("command " + JSON.stringify(envelope));

    let responsePromise = null;
    if (waitForResponse) {
      responsePromise = new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          pending.delete(envelope.command_id);
          reject(new Error(`Timed out waiting for ${scenario.command}.`));
        }, RESPONSE_TIMEOUT_MILLISECONDS);
        pending.set(envelope.command_id, { resolve, reject, timeout });
      });
    }

    host.sendCommand(envelope);
    return responsePromise;
  }

  function renderScenarios() {
    actions.replaceChildren();
    for (const scenario of catalog.scenarios) {
      const button = documentRef.createElement("button");
      button.type = "button";
      button.dataset.scenarioId = scenario.id;
      button.append(documentRef.createTextNode(scenario.label));
      const command = documentRef.createElement("small");
      command.textContent = scenario.command;
      button.append(command);
      button.onclick = () => sendScenario(scenario).catch(error =>
        write("harness error " + error.message));
      actions.append(button);
    }
  }

  async function runAutomatic() {
    runButton.disabled = true;
    try {
      await waitUntilReady();
      const scenarios = catalog.scenarios.filter(
        scenario => scenario.run_automatically);
      let passed = 0;
      for (const scenario of scenarios) {
        const response = await sendScenario(scenario, true);
        if (Boolean(response.success) !== Boolean(scenario.expected_success)) {
          throw new Error(
            `${scenario.command} returned success=${response.success}; ` +
            `expected ${scenario.expected_success}.`);
        }
        passed += 1;
      }
      write(`automated scenarios passed ${passed}/${scenarios.length}`);
      return passed;
    } catch (error) {
      write("automated scenarios failed " + error.message);
      throw error;
    } finally {
      runButton.disabled = false;
    }
  }

  runButton.onclick = () => runAutomatic().catch(() => {});
  reloadButton.onclick = () => iframe.contentWindow.location.reload();
  renderScenarios();
  write(`loaded ${catalog.scenarios.length} generated scenarios`);

  const dispose = () => {
    if (disposed) return;
    disposed = true;
    for (const waiting of pending.values()) {
      clearTimeout(waiting.timeout);
      waiting.reject(new Error("The harness was disposed."));
    }
    pending.clear();
    host.dispose();
  };
  windowRef.addEventListener("beforeunload", dispose, { once: true });

  return Object.freeze({ catalog, host, runAutomatic, sendScenario, dispose });
}

async function readJson(fetchImpl, url) {
  const response = await fetchImpl(url, { cache: "no-store" });
  if (!response.ok) throw new Error(`Could not load ${url}.`);
  return response.json();
}

function validateCatalog(catalog) {
  if (!catalog || catalog.schema_version !== 1 ||
      !Array.isArray(catalog.scenarios) || catalog.scenarios.length === 0) {
    throw new Error("The command harness catalog is invalid.");
  }
  const ids = new Set();
  for (const scenario of catalog.scenarios) {
    if (!scenario?.id || !scenario?.command || ids.has(scenario.id)) {
      throw new Error("The command harness catalog contains an invalid scenario.");
    }
    ids.add(scenario.id);
  }
}

export function resolveCatalogTransportId(catalog) {
  const configured = catalog?.transport_id;
  if (configured === undefined) {
    return "web-viewer";
  }
  if (typeof configured !== "string") {
    throw new Error("The command harness transport ID is invalid.");
  }
  const normalized = configured.trim();
  if (!normalized || normalized.length > 96) {
    throw new Error("The command harness transport ID is invalid.");
  }
  return normalized;
}

if (typeof window !== "undefined" && typeof document !== "undefined" &&
    !globalThis.__DEUCARIAN_HARNESS_NO_AUTO_START__) {
  window.DeucarianHarnessReady = startHarness().catch(error => {
    const output = document.querySelector("#events");
    if (output) output.textContent = "Harness failed: " + error.message;
    throw error;
  });
}
