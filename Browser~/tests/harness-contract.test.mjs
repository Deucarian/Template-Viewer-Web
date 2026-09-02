import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

globalThis.__DEUCARIAN_HARNESS_NO_AUTO_START__ = true;
const {
  createScenarioEnvelope,
  materializePayload,
  resolveCatalogTransportId,
  startHarness
} = await import("../harness.js");

const catalog = JSON.parse(await readFile(
  new URL("../commands.generated.json", import.meta.url),
  "utf8"));

class FakeWindow extends EventTarget {
  constructor() {
    super();
    this.location = {
      origin: "http://localhost:8080",
      href: "http://localhost:8080/harness.html",
      search: ""
    };
    this.posts = [];
    this.onPost = null;
  }

  postMessage(message, targetOrigin) {
    this.posts.push({ message, targetOrigin });
    this.onPost?.(message, targetOrigin);
  }
}

class FakeElement extends EventTarget {
  constructor() {
    super();
    this.children = [];
    this.dataset = {};
    this.disabled = false;
    this.textContent = "";
  }

  append(...values) {
    this.children.push(...values);
  }

  replaceChildren(...values) {
    this.children = [...values];
  }
}

class FakeIframe extends FakeElement {
  constructor(contentWindow) {
    super();
    this.contentWindow = contentWindow;
    this.src = "";
  }
}

function messageEvent(source, origin, data) {
  const event = new Event("message");
  Object.defineProperties(event, {
    source: { value: source },
    origin: { value: origin },
    data: { value: data }
  });
  return event;
}

function createDocument(iframe) {
  const elements = {
    actions: new FakeElement(),
    events: new FakeElement(),
    "run-automatic": new FakeElement(),
    "reload-viewer": new FakeElement(),
    "connection-state": new FakeElement(),
    viewer: iframe
  };
  return {
    elements,
    querySelector(selector) {
      return elements[selector.slice(1)];
    },
    createElement() {
      return new FakeElement();
    },
    createTextNode(value) {
      return value;
    }
  };
}

const fetchImpl = async url => ({
  ok: true,
  async json() {
    return url.includes("harness-config")
      ? { viewer_path: "/mock-viewer.html" }
      : catalog;
  }
});

test("generated catalog represents every generic Unity command", () => {
  assert.equal(catalog.schema_version, 1);
  assert.deepEqual(
    [...new Set(catalog.scenarios.map(value => value.command))].sort(),
    [
      "clear_access_token",
      "clear_selection",
      "dispose_viewer",
      "initialize_viewer",
      "refresh_access_token",
      "select_elements",
      "update_access_token",
      "updateaccesstoken"
    ]);
  assert.equal(
    catalog.scenarios.filter(value => value.run_automatically).length,
    7);
  assert.ok(catalog.scenarios.every(value => value.payload));
});

test("scenario envelopes materialize current and stale revisions", () => {
  assert.deepEqual(materializePayload({
    current: "$revision",
    nested: ["$stale_revision", "literal"]
  }, 8), {
    current: 8,
    nested: [6, "literal"]
  });
  assert.deepEqual(
    createScenarioEnvelope({
      id: "select-red",
      command: "select_elements",
      payload: { revision: "$revision", element_ids: ["red"] }
    }, 3, 9),
    {
      protocol_version: 1,
      command_id: "harness-3-select-red",
      command: "select_elements",
      payload: { revision: 9, element_ids: ["red"] },
      metadata: { source: "local-harness" }
  });
});

test("product catalogs route their exact Activity and Report transport IDs", async () => {
  for (const transportId of ["activity-viewer", "report-viewer"]) {
    const browserWindow = new FakeWindow();
    const viewerWindow = new FakeWindow();
    const iframe = new FakeIframe(viewerWindow);
    const documentRef = createDocument(iframe);
    const productCatalog = { ...catalog, transport_id: transportId };
    const productFetch = async url => ({
      ok: true,
      async json() {
        return url.includes("harness-config")
          ? { viewer_path: "/viewer/index.html" }
          : productCatalog;
      }
    });

    const harness = await startHarness({
      windowRef: browserWindow,
      documentRef,
      fetchImpl: productFetch
    });
    assert.equal(
      viewerWindow.posts.at(-1).message.transport_id,
      transportId);
    harness.dispose();
  }
});

test("catalog transport validation has only the generic compatibility fallback", () => {
  assert.equal(resolveCatalogTransportId({}), "web-viewer");
  assert.equal(
    resolveCatalogTransportId({ transport_id: " report-viewer " }),
    "report-viewer");
  assert.throws(
    () => resolveCatalogTransportId({ transport_id: "" }),
    /transport ID is invalid/);
  assert.throws(
    () => resolveCatalogTransportId({ transport_id: null }),
    /transport ID is invalid/);
  assert.throws(
    () => resolveCatalogTransportId({ transport_id: "x".repeat(97) }),
    /transport ID is invalid/);
});

test("the harness renders the catalog and runs its automated scenarios", async () => {
  const browserWindow = new FakeWindow();
  const viewerWindow = new FakeWindow();
  const iframe = new FakeIframe(viewerWindow);
  const documentRef = createDocument(iframe);
  const harness = await startHarness({
    windowRef: browserWindow,
    documentRef,
    fetchImpl
  });
  const probe = viewerWindow.posts.at(-1).message;
  assert.equal(probe.type, "deucarian-command-probe");

  viewerWindow.onPost = message => {
    if (message.type !== "deucarian-command") return;
    const envelope = message.message;
    const scenarioId = envelope.command_id.replace(/^harness-\d+-/, "");
    const scenario = catalog.scenarios.find(value => value.id === scenarioId);
    queueMicrotask(() => browserWindow.dispatchEvent(messageEvent(
      viewerWindow,
      browserWindow.location.origin,
      {
        source: "deucarian-command-transport",
        type: "deucarian-command-response",
        transport_id: "web-viewer",
        connection_generation: 1,
        host_session: probe.host_session,
        message: {
          protocol_version: 1,
          command_id: envelope.command_id,
          command: envelope.command,
          success: scenario.expected_success
        }
      })));
  };
  browserWindow.dispatchEvent(messageEvent(
    viewerWindow,
    browserWindow.location.origin,
    {
      source: "deucarian-command-transport",
      type: "deucarian-command-ready",
      transport_id: "web-viewer",
      connection_generation: 1,
      host_session: probe.host_session
    }));

  assert.equal(
    documentRef.elements.actions.children.length,
    catalog.scenarios.length);
  assert.equal(
    documentRef.elements["connection-state"].dataset.ready,
    "true");
  assert.equal(await harness.runAutomatic(), 7);
  assert.match(
    documentRef.elements.events.textContent,
    /automated scenarios passed 7\/7/);
  assert.ok(viewerWindow.posts.filter(entry =>
    entry.message.type === "deucarian-command").every(entry =>
      entry.targetOrigin === browserWindow.location.origin));
  harness.dispose();
});
