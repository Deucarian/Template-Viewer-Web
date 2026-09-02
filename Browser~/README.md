# Local browser harness

This package-owned harness exercises the production Command Routing
`postMessage` host through a real same-origin iframe. It needs no deployment,
credentials, backend, or sibling source checkout.

Run it with the included mock Unity iframe:

```powershell
npm start
```

Then open `http://localhost:8080/harness.html`. The mock accepts the canonical
transport handshake and command envelopes, allowing iframe lifecycle,
generated actions, revision substitution, response matching, and the complete
automated sequence to be tested before a Unity WebGL build exists.

To exercise a real development build instead:

```powershell
npm start -- --build C:/path/to/WebGLBuild --catalog C:/path/to/project/Library/Deucarian/WebViewerHarness/commands.generated.json
```

The server exposes the build below `/viewer`, carries the generated product
transport ID into the parent host, and injects its exact loopback
`window.location.origin` into the child before Unity starts. The parent and
iframe therefore retain one explicit same origin without a wildcard,
`document.referrer`, or serialized localhost fallback. Opening the HTML
directly does not exercise the secured parent/iframe path.

## Generated commands

The checked-in `commands.generated.json` describes the generic sample. The
template's **Sync Profiles** action writes its development scene catalog to:

```text
Library/Deucarian/WebViewerHarness/commands.generated.json
```

Generated product catalogs carry the scene bootstrap's exact `transport_id`.
For compatibility, an older catalog with no `transport_id` is treated only as
the generic sample and uses `web-viewer`; invalid or mismatched IDs fail
instead of selecting another product transport.

Serve a consumer catalog with:

```powershell
npm start -- --build C:/path/to/WebGLBuild --catalog C:/path/to/project/Library/Deucarian/WebViewerHarness/commands.generated.json
```

The generator reads the same handler composition used by
`WebViewerBootstrap`. Every registered command therefore receives a manual
action without maintaining a second command-name list. Generic commands have
safe representative payloads and automated expectations. A
`Deucarian.TemplateViewer.ViewerFeatureBehaviour` can override
`CreateCommandHarnessScenarios()` to
add valid product examples. Commands without an example remain visible but do
not join the automated run.

The package-owned product build workflow calls
`WebViewerCommandHarnessCatalogGenerator.GenerateForScene(scenePath)` for the
real scene declared by `WebViewerProductBuildDefinition`. Products do not need
a custom provider or a second editor window.

Authentication update examples never store or generate a real token and are
excluded from automation. The harness contains no credentials or production
endpoint.

## Validation

```powershell
npm test
```

The tests exercise the distributable canonical host, generated catalog,
automated runner, exact-origin behavior, mock iframe contract, and local HTTP
server.
