import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(
  new URL(
    "../../Runtime/Plugins/WebGL/DeucarianWebViewer.jslib",
    import.meta.url),
  "utf8");

test("deployment iframe configuration uses one exact explicit origin", () => {
  assert.match(source, /window\.deucarianWebViewerConfig/);
  assert.match(source, /config\.parentOrigin \|\| config\.parent_origin/);
  assert.match(source, /url\.origin/);
  assert.match(source, /raw === "\*"/);
  assert.match(source, /url\.pathname !== "\/"/);
  assert.doesNotMatch(source, /document\.referrer/);
});

test("embedding detection distinguishes top-level and parent iframe pages", () => {
  assert.match(source, /window\.parent !== window/);
  assert.match(source, /DeucarianWebViewerIsParentIframe/);
  assert.match(source, /DeucarianWebViewerGetConfiguredParentOrigin/);
});
