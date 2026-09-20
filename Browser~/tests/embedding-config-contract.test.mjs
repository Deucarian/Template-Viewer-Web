import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

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

test("existing embedding configuration keeps exact-origin validation", () => {
  const library = {};
  const window = { simultriaWebViewerConfig: { parentOrigin: "https://host.example" } };
  vm.runInNewContext(source, {
    window, URL, LibraryManager: { library },
    mergeInto: (target, entries) => Object.assign(target, entries)
  });
  const helper = library.$DeucarianWebViewerBrowserInterop;
  const resolve = () => helper.normalizeExplicitOrigin(helper.getViewerConfig().parentOrigin);
  assert.equal(resolve(), "https://host.example");
  for (const invalid of ["*", "https://host.example/path", "https://host.example?token=secret", "https://user:password@host.example"]) {
    window.simultriaWebViewerConfig.parentOrigin = invalid;
    assert.equal(resolve(), null);
  }
  window.simultriaWebViewerConfig.parentOrigin = "https://host.example";
  window.deucarianWebViewerConfig = { parentOrigin: "*" };
  assert.equal(resolve(), null, "invalid explicit current configuration must not fall back");
});
