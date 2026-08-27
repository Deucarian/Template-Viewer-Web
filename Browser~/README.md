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
npm start -- --build C:/path/to/WebGLBuild
```

The development viewer scene must use iframe mode with parent origin
`http://localhost:8080`. The server exposes the build below `/viewer` so the
parent and iframe retain the same exact origin. Opening the HTML directly does
not exercise production origin checks.

## Generated commands

The checked-in `commands.generated.json` describes the generic sample. The
template's **Sync Profiles** action writes its development scene catalog to:

```text
Library/Deucarian/WebViewerHarness/commands.generated.json
```

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

Product build providers call
`WebViewerCommandHarnessCatalogGenerator.GenerateForScene(scenePath)` from
their existing synchronization action. This keeps catalog generation inside
the shared package without adding another project-specific editor window.

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
