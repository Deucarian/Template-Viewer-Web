# Deucarian Web Viewer Adapter

`com.deucarian.template.viewer.web` connects the platform-neutral
`com.deucarian.template.viewer` application to a WebGL browser host. It owns
only the browser boundary:

- secure direct-page and exact-origin iframe command transport;
- browser event publication and WebGL page lifecycle/progress projection;
- a runnable Web sample and local iframe harness; and
- project-owned development and production WebGL Build Profiles.

Model loading, initialization, selection, authentication, diagnostics,
navigation, rendering, and the in-viewer shell are shared by the generic core.
`WebViewerBootstrap` derives from the core `ViewerBootstrap` and retains its
existing Unity component identity, so existing scenes keep working after the
split.

## One viewer, multiple platforms

A product can use one Unity project and one set of
`ViewerFeatureBehaviour` components for Web, desktop, and XR. Install the
generic core plus the adapters that product needs, then use a separate
bootstrap scene and Build Profile for each target. Each player contains exactly
one platform adapter: WebGL uses this package, while desktop and XR use their
own adapter packages. The Web assembly is included only in Editor and WebGL
players, so it does not become a desktop or XR runtime dependency.

## Quick start

1. Import the **Web Viewer** sample and open `Scenes/WebViewer.unity`.
2. Enter Play Mode. Local Editor commands use the same generic application as
   WebGL builds.
3. Open `Tools > Deucarian > Communication > Command Routing`, choose
   **Live Tester**, and run a generated scenario.
4. In the Deucarian Build Pipeline Manager, synchronize the **Web Viewer
   Template** provider to create project-owned development and production
   scenes and WebGL Build Profiles.
5. Run `npm start` in `Browser~` to exercise the mock viewer, or pass a WebGL
   build to the same local server for an end-to-end run.

Product packages extend the shared application with
`Deucarian.TemplateViewer.ViewerFeatureBehaviour`. They can contribute command
handlers, a typed initialization handler, visibility ownership, and safe local
harness scenarios without depending on browser transport types.

## Browser modes

Top-level localhost and Editor workflows use direct-page mode. A browser iframe
automatically uses deployment-owned secure iframe mode. Before the Unity loader
starts, the deployment page must provide one exact backoffice origin:

```html
<script>
  window.deucarianWebViewerConfig = {
    parentOrigin: "https://backoffice.example.com"
  };
</script>
```

The adapter never sends to `*`. An embedded build with a missing, wildcard,
path-bearing, credential-bearing, or otherwise invalid origin fails closed.
Serialized localhost settings are available only for the explicit local iframe
harness and are never used as a deployed iframe fallback. Production
validation requires non-loopback HTTPS.

The command and event names are owned by the generic core and remain unchanged:
`initialize_viewer`, `select_elements`, `clear_selection`, `dispose_viewer`,
the authentication commands, and the corresponding viewer lifecycle and
selection events. Browser transport readiness is distinct from application
`viewer_ready`; the latter occurs only after the shared application is ready.

## Local browser harness

Run the package-owned harness with the included mock:

```powershell
npm start
```

Then open `http://localhost:8080/harness.html`. To serve a real development
build and a product-generated catalog:

```powershell
npm start -- --build C:/path/to/WebGLBuild --catalog C:/path/to/project/Library/Deucarian/WebViewerHarness/commands.generated.json
```

`WebViewerCommandHarnessCatalogGenerator` derives its catalog from the actual
generic command handlers and scene feature components, so the browser harness
and the Unity Live Tester use the same command composition.

## Validation

Run the Package Registry validator, Unity EditMode tests, `npm test` in
`Browser~`, and `git diff --check`.

## License

See [LICENSE.md](LICENSE.md).
