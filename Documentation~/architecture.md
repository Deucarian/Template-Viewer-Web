# Architecture

This package is a platform adapter, not a viewer application fork.

```text
product ViewerFeatureBehaviour components
                 |
                 v
Deucarian.TemplateViewer.ViewerBootstrap
  -> shared application, commands, loading, visibility, navigation and shell
                 |
                 v
Deucarian.TemplateViewerWeb.WebViewerPlatformAdapter
  -> WebGL command transport
  -> exact browser event endpoint
  -> WebGL page lifecycle/progress sink
```

`WebViewerBootstrap` is the scene-facing compatibility component. It inherits
all generic serialized viewer and authentication fields from
`ViewerBootstrap`, adds only `iframeMode`, `parentOrigin`, and `transportId`,
explicitly enables the core's optional model reveal, and creates one Web
platform adapter. The reveal instance, interruption relay, product-readiness
composition, and disposal remain owned by the core; the Web package adds no
second lifecycle owner or serialized reveal state.

The adapter delays browser transport activation until the core has composed
the application and command runtime. Its activation lease owns the transport
bridge and releases listeners and diagnostics idempotently. The event publisher
uses the same transport and exact endpoint as command responses. The lifecycle
sink projects generic state into the package-owned WebGL page shell; it does
not implement a second in-viewer status UI.

The runtime and sample assemblies compile only for Editor and WebGL. A
multi-platform product can therefore install Web, desktop, and XR adapters in
one project while selecting one adapter bootstrap per target-specific scene and
Build Profile.

There is no runtime reflection or service locator. Editor-only discovery is
limited to the documented Build Pipeline provider and command test catalog
registries.
