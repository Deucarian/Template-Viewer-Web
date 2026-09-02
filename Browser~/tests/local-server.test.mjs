import assert from "node:assert/strict";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import {
  createHarnessServer,
  parseArguments
} from "../local-server.mjs";

test("local server hosts the harness, generated catalog, and mock iframe", async () => {
  const server = createHarnessServer();
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const origin = `http://127.0.0.1:${address.port}`;
  try {
    const configuration = await fetch(
      origin + "/harness-config.generated.json");
    assert.equal(configuration.status, 200);
    assert.deepEqual(await configuration.json(), {
      viewer_path: "/mock-viewer.html"
    });

    for (const path of [
      "/harness.html",
      "/harness.js",
      "/commands.generated.json",
      "/mock-viewer.html"
    ]) {
      const response = await fetch(origin + path);
      assert.equal(response.status, 200, path);
      assert.equal(response.headers.get("cache-control"), "no-store");
    }

    const traversal = await fetch(origin + "/..%2Fpackage.json");
    assert.equal(traversal.status, 404);
  } finally {
    await new Promise(resolve => server.close(resolve));
  }
});

test("real build index receives exact loopback parent origin before Unity starts", async () => {
  const buildRoot = await mkdtemp(join(tmpdir(), "deucarian-web-build-"));
  const catalogPath = join(buildRoot, "commands.product.json");
  await writeFile(
    join(buildRoot, "index.html"),
    "<!doctype html><html><head>" +
      "<script>window.unityBootstrapSaw = " +
      "window.deucarianWebViewerConfig;</script>" +
      "</head><body>Unity</body></html>",
    "utf8");
  await writeFile(
    catalogPath,
    JSON.stringify({
      schema_version: 1,
      transport_id: "report-viewer",
      default_scenario_id: "",
      scenarios: []
    }),
    "utf8");
  const documentedProductArguments = parseArguments([
    "--build",
    buildRoot,
    "--catalog",
    catalogPath
  ]);
  const server = createHarnessServer(documentedProductArguments);
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const origin = `http://127.0.0.1:${address.port}`;
  try {
    const configuration = await fetch(
      origin + "/harness-config.generated.json");
    assert.deepEqual(await configuration.json(), {
      viewer_path: "/viewer/index.html"
    });

    const catalogResponse = await fetch(
      origin + "/commands.generated.json");
    assert.equal(catalogResponse.status, 200);
    assert.equal(
      (await catalogResponse.json()).transport_id,
      "report-viewer");

    const response = await fetch(origin + "/viewer/index.html");
    assert.equal(response.status, 200);
    assert.equal(response.headers.get("cache-control"), "no-store");
    const source = await response.text();
    const configurationOffset = source.indexOf(
      "parentOrigin: window.location.origin");
    const unityOffset = source.indexOf("window.unityBootstrapSaw");
    assert.ok(configurationOffset >= 0);
    assert.ok(configurationOffset < unityOffset);
    assert.match(source, /hostname !== "localhost"/);
    assert.doesNotMatch(source, /document\.referrer|parentOrigin:\s*"\*"/);
  } finally {
    await new Promise(resolve => server.close(resolve));
    await rm(buildRoot, { recursive: true, force: true });
  }
});
