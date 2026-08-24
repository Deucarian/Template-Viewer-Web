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
  development authentication menu; Session API Integration supplies the
  credential-free endpoint profile and response mapping used by that menu;
- Diagnostics reports sanitized lifecycle, revision, and element counts; and
- Build Pipeline owns the shared policy while this template supplies the
  project-specific development/production provider and profiles; and
- WebGL Template supplies the browser page, loading/ready/failure states, and
  lifecycle bridge shared by browser-hosted viewers.

Product packages may add `WebViewerFeatureBehaviour` components beside the
bootstrap. They can contribute commands and one replaceable visibility owner.
When a product owns visibility, the generic `select_elements` controller is not
created, so two systems never compete over model active states. Model loading,
navigation, camera state, browser transport, and shell behavior stay shared.

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

An optional connection package can register one vendor-neutral runtime
connection through Viewer Authentication. When exactly one provider resolves,
the template reuses its stable authentication target, session, API client,
configured API base URL, and authenticated model origins. The template does not
create a second target or copy its token. With no provider it retains the
generic, template-owned authentication composition below. A failed provider or
multiple active providers stop initialization; the template never silently
falls back to a different session after a connection was requested.

For an authenticated development session, open **Tools > Deucarian > Viewer >
Authentication**. Paste/replace input is masked and cleared immediately. An
optional remembered token is stored only in this Unity project's local
`UserSettings`, not in the template package or a versioned ScriptableObject.
Assign a credential-free `SessionTokenEndpointProfile` on `WebViewerBootstrap`,
or place one at Resources path
`Deucarian/ViewerAuthenticationTokenEndpointProfile`. The shared window then
renders its transient fields and offers **Refresh Token**, which reacquires a
token through the configured endpoint. Credentials remain window-local and the
profile stores only request/response shape. A true automatic refresh service is
still a separate optional capability.

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

The composition root also injects its runtime into Command Routing's
scene-owned local ingress. Optional editor connection packages can therefore
send this exact envelope during Play Mode without depending on the template's
application types or bypassing command handlers.

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

Model downloads use Object Loading API Integration's shared trusted-origin
policy. API-relative URLs are first resolved to a canonical absolute URL against
the active connection's API base. Absolute URLs must match that base origin or
an exact additional origin explicitly allowlisted on `WebViewerBootstrap`.
Trusted destinations use the optional live session provider. Untrusted absolute
HTTP(S) URLs remain supported as explicitly anonymous public downloads, including
when no API base is configured. Invalid URLs are rejected. Origin matching
includes the exact scheme, host, and effective port, and origin entries
containing paths, user information, queries, fragments, or wildcards are
invalid.

An optional runtime connection contributes its own validated API base and exact
authenticated origins to the shared policy. Connection packages pass URLs and
version metadata only; bearer tokens never belong in `model_url`, command
payloads, query strings, or diagnostics.

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
- define a credential-free Session API token endpoint profile when the shared
  Authentication menu should offer endpoint-backed Refresh Token.
- install an optional runtime connection provider when a backend integration
  should supply one stable authentication session, API client, and trusted
  model-download origins without product-local bootstrap code.

## Validation

Run the Package Registry validator, Unity EditMode/PlayMode tests, browser tests
with `npm test` in `Browser~`, and `git diff --check`.

## License

See [LICENSE.md](LICENSE.md).
