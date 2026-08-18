# Deucarian Web Viewer Template

`com.deucarian.template.viewer.web` is a ready-to-run generic starting point
for browser-hosted Unity viewers. It uses the reusable Deucarian stack instead
of copying a viewer application:

- Viewer Navigation supplies Orbit/Fly, Top Down, Return to Origin, the polished
  shared-reference icon toolbar and interactions, pointer/input coordination,
  browser reduced-motion handling, and an optional six-face view cube that is
  off by default. With no intentional
  override, it loads the package's complete reference navigation composition,
  including its canonical dark Frosted Glass theme and theme provider. The
  toolbar's UI Toolkit assets, control-island chrome, element tree, pointer
  behavior, and movement-key suppression are package-owned; template consumers
  must not recreate or restyle that presentation locally;
- UI supplies the canonical runtime PanelSettings, semantic surface roles, and
  shared topmost tooltip overlay used by every in-viewer UI package;
- Viewer Rendering supplies the exact reference camera, key light, URP,
  post-processing, reflection, themed environment, display-settings state, and
  semantic Full quality baseline independently of consumer quality indices;
- Viewer Shell supplies the complete Report-donor generic status/toast,
  information and display-settings menus, responsive chrome, input boundaries,
  menu coordination, and tooltips. The template contains only a lifecycle
  adapter and no local shell presentation;
- Command Routing and its WebGL Integration supply canonical envelopes,
  direct-page and secure iframe transport, ready handshake, and cleanup;
- API, Object Loading, and their integration load AssetBundle content;
- Viewer Authentication composes Session with the live API auth provider,
  standard update/refresh/clear commands, sanitized status, and the shared
  development authentication menu;
- Diagnostics reports sanitized lifecycle, revision, and element counts; and
- Build Pipeline owns the shared policy while this template supplies the
  project-specific development/production provider and profiles.

## Quick start

1. Import the **Web Viewer** sample and open `Scenes/WebViewer.unity`.
2. Enter Play Mode. The bootstrap creates an embedded three-element model and
   waits for `initialize_viewer`.
3. For a WebGL build, open the Deucarian Build Pipeline Manager and choose
   **Sync Profiles** for the Web Viewer Template provider.
4. Build **Development** and serve the self-contained `Browser~` harness at
   `http://localhost:8080`.

The sample is credential-free and defaults to an embedded model. A host can
instead supply an HTTP(S) or API-relative `model_url`. Replace
`IWebViewerModelDescriptorResolver` in the composition root when an application
must resolve a project/model/version context. Do not put backend DTOs in this
template.

For an authenticated development session, open **Tools > Deucarian > Viewer >
Authentication**. Paste/replace input is masked and cleared immediately. An
optional remembered token is stored only in this Unity project's local
`UserSettings`, not in the template package or a versioned ScriptableObject.
The same window can refresh when the application supplied a real backend
refresh adapter, and shows a Get/Sign In action when a backend-specific token
acquisition provider is registered.

## Commands

All commands use the canonical Command Routing envelope:

```json
{
  "protocol_version": 1,
  "command_id": "host-42",
  "command": "initialize_viewer",
  "payload": { "revision": 1 },
  "metadata": { "source": "host" }
}
```

Supported generic commands:

- `initialize_viewer`: `revision`, optional `model_url`, `model_id`,
  `model_version`, `cache_version`, and `cache_hash`;
- `select_elements`: `revision` and one or more stable `element_ids`;
- `clear_selection`: `revision`, restoring the captured visibility baseline;
- `dispose_viewer`: `revision`, unloading the model and cancelling work.
- `update_access_token`: `access_token` and optional UTC expiry, replacing the
  live viewer session without reconstructing API clients;
- `updateaccesstoken`: compatibility alias for existing viewer hosts;
- `refresh_access_token`: asks the configured Session refresh adapter for a
  new token;
- `clear_access_token`: clears the active viewer session.

The browser receives `viewer_loading`, application-level `viewer_ready`,
`viewer_failed`, `selection_applied`, `viewer_disposed`, and sanitized
`access_token_updated`, `access_token_refreshed`, and `access_token_cleared`
events. Authentication events contain lifecycle status only and never include
the access token. Transport
readiness only means listeners are installed; `viewer_ready` is emitted after
model loading, identifier indexing, and navigation reference/origin capture.

## State and camera guarantees

`WebViewerSelectionStateOwner` is authoritative for generic selection. Newer
revisions supersede older state; stale revisions and unknown IDs preserve the
last valid visibility plan. Clearing restores the baseline captured after load.

Selection updates only call `WebViewerVisibilityController`. They never call
Viewer Navigation, so camera transform, projection, pivot, navigation mode,
and current user position remain unchanged. Initial model registration frames
once and captures Return to Origin after model placement.

`WebViewerBootstrap.ResolvedNavigationComposition`,
`ResolvedRenderingComposition`, and `ResolvedShellProfile` expose the exact
shared compositions used at runtime. `NavigationInstaller`,
`RenderingInstaller`, and `ShellPresenter` all expose the same authoritative
theme provider. Supplying custom navigation settings only replaces the preset;
input, bounds, animation, rendering, shell, and theme policies stay shared.
The toolbar resolves colors, visual style, and theme mode through
`com.deucarian.theming`; the template does not contain a parallel theme palette.
Its public element names come from `ViewerNavigationToolbarPresenter`, so host
automation can locate controls without taking ownership of their hierarchy or
presentation.

## Shared UI layering

`com.deucarian.ui` is the single authority for in-viewer UI depth. Viewer
Navigation requests `PrimaryControls`, Viewer Shell requests `Status` and
`Menu`, and runtime tooltips use `Tooltip`. Consumers compose those
roles through `DeucarianUIRuntime`; they do not assign numeric sorting orders,
create private PanelSettings assets, or implement their own tooltip overlays.

All in-viewer surfaces use the canonical UI Toolkit panel family, so the
topmost tooltip guarantee is enforced by one compositing system. Feature
packages still own their content and behavior, while UI owns how their surfaces
are composed relative to one another.

## Browser security

Direct-page mode uses same-page events. Iframe mode requires an exact HTTP(S)
allowed and target origin, validates the parent source window, and never sends
to `*`. Production validation additionally requires a non-loopback HTTPS origin.
The host owns the Unity instance and disposes its listeners on teardown.

Model downloads use the live session provider only for API-relative URLs,
URLs on the configured API origin, or exact additional origins explicitly
allowlisted on `WebViewerBootstrap`. Other cross-origin URLs are deliberately
anonymous so a host-supplied URL cannot receive the viewer credential.

## Build profiles

`WebViewerBuildManagerProvider` is discovered by Deucarian Build Pipeline. Its
explicit **Sync Profiles** action creates project-owned scenes and WebGL Build
Profile assets under `Assets/Deucarian/WebViewer` and applies the shared dev or
production policy. Production validation rejects local/insecure iframe origins;
Build Pipeline excludes development diagnostics and development-context files.

## Extension points

- implement `IWebViewerModelDescriptorResolver` for application API/model
  version resolution;
- implement `IWebViewerModelLoader` only when Object Loading cannot represent
  the source;
- replace the example `WebViewerElement` index/controller with a domain-owned
  visibility capability;
- add application commands through Command Routing handlers, not the transport.
- implement the Viewer Authentication acquisition provider in a
  backend-specific package when the shared menu should offer Get/Sign In.

## Validation

Run the Package Registry validator, Unity EditMode/PlayMode tests, browser tests
with `npm test` in `Browser~`, and `git diff --check`.

## License

See [LICENSE.md](LICENSE.md).
