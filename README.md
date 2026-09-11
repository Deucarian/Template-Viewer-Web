# Deucarian Web Viewer Adapter

`com.deucarian.template.viewer.web` connects the platform-neutral
`com.deucarian.template.viewer` application to a WebGL browser host. It owns
only the browser boundary:

- secure direct-page and exact-origin iframe command transport;
- browser event publication and WebGL page lifecycle/progress projection;
- a runnable Web sample and local iframe harness; and
- one package-owned development/production WebGL workflow targeting either a
  declarative real product scene or the backward-compatible generic sample.

Model loading, initialization, selection, authentication, diagnostics,
navigation, rendering, and the in-viewer shell are shared by the generic core.
`WebViewerBootstrap` derives from the core `ViewerBootstrap` and retains its
existing Unity component identity, so existing scenes keep working after the
split. It explicitly enables the core's optional lifecycle-safe model reveal;
custom and non-Web bootstraps remain unchanged, and the Web adapter introduces
no second readiness or lifecycle owner.

## One viewer, multiple platforms

A product can use one Unity project and one set of
`ViewerFeatureBehaviour` components for Web, desktop, and XR. Install the
generic core plus the adapters that product needs, then use a separate
bootstrap scene and Build Profile for each target. Each player contains exactly
one platform adapter: WebGL uses this package, while desktop and XR use their
own adapter packages. The Web assembly is included only in Editor and WebGL
players, so it does not become a desktop or XR runtime dependency.

## Quick start

Update this adapter, Viewer Template and their shared dependencies together.
The current viewer baseline uses Camera Navigation 0.3.0, Viewer Navigation
0.2.0 and Theming 1.7.0. The adapter does not recreate their controls or editor
previews. The navigation integration enables **Both** input backends; restart
Unity if requested. Existing product scenes and settings remain project-owned.
Previously imported sample scenes are independent copies, not live links to
the package's `Samples~` folder.

1. Import the **Web Viewer** sample and open `Scenes/WebViewer.unity`.
2. Enter Play Mode. Local Editor commands use the same generic application as
   WebGL builds.
3. Open `Tools > Deucarian > Control Center...`, choose **Communication >
   Command Routing**, then choose **Live Tester** and run a generated scenario.
4. In the Deucarian Build Pipeline Manager, synchronize the **Web Viewer
   Template** provider to create project-owned development and production
   scenes and WebGL Build Profiles. A product definition makes the same
   provider target the product's existing real scene instead.
5. Run `npm start` in `Browser~` to exercise the mock viewer, or pass a WebGL
   build to the same local server for an end-to-end run.

Product packages extend the shared application with
`Deucarian.TemplateViewer.ViewerFeatureBehaviour`. They can contribute command
handlers, a typed initialization handler, visibility ownership, and safe local
harness scenarios without depending on browser transport types.

## Declarative product workflow

A browser viewer product creates exactly one
`WebViewerProductBuildDefinition` at:

```text
Assets/Deucarian/WebViewer/Editor/WebViewerProductBuildDefinition.asset
```

The asset declares only its stable provider/display/transport identity, real
scene, Development and Production versions/output paths, and exactly one
domain feature. Required TMP readiness and exact domain-feature cardinality
are package-owned. The package then owns the rest:

- the two canonical profiles under
  `Assets/Deucarian/WebViewer/BuildProfiles`;
- real-scene selection and Web bootstrap validation;
- WebGL policy, template, run-in-background, HTTP, and version settings;
- command-harness generation;
- shared reference-theme validation and browser first-paint export; and
- Build Pipeline lifecycle validation, reversible contributor scopes, and
  post-build artifact validation supplied by the installed packages.

When this asset exists, no generic fallback scene or target is generated. The
real product scene remains the Editor/Play Mode test entry point; only build
orchestration moves into packages. A product project therefore needs no custom
`IDeucarianBuildManagerProvider`. Existing CI facades can forward to
`WebViewerProductBuildApi.Synchronize`, `BuildDevelopment`, or
`BuildProduction` for one migration release.

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

## Early startup and optional Simultria status

The Web bootstrap creates its page status sink before resolving the deployment
parent origin. Composition failures therefore reach the shared loading page
even before command transport or Unity UI exists. The same sink is reused for
normal application lifecycle. Safe failure codes, not exception text, cross
this boundary. Template Viewer 0.3.2 and WebGL Template 0.1.1 are required.

When Simultria Viewer Connection 1.2.1 or newer is installed, the version-defined
`Deucarian.TemplateViewerWeb.SimultriaIntegration` assembly adds
`SimultriaWebViewerStartupBridge`. This is an optional dependency; installations
without Connection, or with an older version, compile without the bridge.
API 2.0.2 or newer (already required by Connection 1.2.1) has its own matching
version define and assembly constraint. The runtime bridge and its optional
test assemblies require both guards; API remains optional for this adapter.
The bridge requires the additive SIM-866 startup snapshot/event API; it does
not duplicate that ticket's directory or fallback policy.

Add the package-owned bridge to the product's real scene and explicitly assign
its `connectionGate` and `viewerBootstrap` fields. Both must have valid same-scene
references, and the gate's startup-behaviour list must contain that bootstrap.
The initial `OnEnable` runs while Unity is still loading the scene; `isLoaded`
is deliberately not a binding requirement. The bridge's read-only `BindingFailure`
reports only a fixed local enum reason for missing references, invalid/different
scenes, or missing explicit ownership. Invalid wiring reports the existing safe
configuration-failure page code, not a backend-connection error or exception text.
Keep the bridge enabled and **out of** the gate's disabled startup-behaviour
list. It runs before the gate, subscribes then replays its current snapshot,
and releases listeners on disable/destroy or gate disposal. It performs no
scene-wide discovery and never enables the viewer or changes routing.

Resolving uses the shared `resolving_environment` progress phase. A gate-selected
fallback uses `build_profile_fallback` plus one of the fixed simple identifiers
`production`, `development`, `testing` or `acceptance`; no host,
version, payload, exception or credential is copied into the page. Exact routing
is not application readiness and does not show a fallback notice. The shared
WebGL Template owns human labels, error/retry presentation, and the genuine
engine-plus-application reveal barrier.

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

Run the Package Registry validator, Unity EditMode and PlayMode tests, `npm test`
in `Browser~`, and `git diff --check`. With Connection 1.2.1 or newer, the optional
`SimultriaWebViewerColdSceneTests` loads a serialized cold scene and verifies real
`OnEnable` guard values, subscription and automatic scene-unload cleanup. Its
inactive gate and disabled viewer prevent network or application startup.

## License

See [LICENSE.md](LICENSE.md).
