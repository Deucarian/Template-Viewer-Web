# Deucarian Web Viewer Template Agent Notes

Package ID: `com.deucarian.template.viewer.web`

Follow Package Registry architecture, dependency, distribution, and release
policies.

## Ownership

This adapter owns the secure browser command/event transport, WebGL page
lifecycle projection, browser harness, runnable Web sample, and package-owned
workflow over project-owned WebGL Build Profiles. Product projects declare
identity, their real scene, build versions/output locations, and exactly one
required domain feature in one canonical definition asset; they do not
implement another build provider. The
platform-neutral Template Viewer package owns
the application composition, model loading, commands, visibility, diagnostics,
authentication, navigation, rendering, and in-viewer shell.

It must not own camera math, raw input, pointer capture, browser transport,
generic command routing, AssetBundle loading internals, Report/Activity DTOs,
or backend-specific model/version lookup.

## Invariants

- Selection changes element visibility only. It never invokes camera movement.
- The shared application publishes `viewer_ready` only after the model, identifier
  index, navigation reference, and command host are ready.
- Revisions are monotonic. Invalid or stale selection keeps the last valid state.
- Reinitialization and disposal release prior loads and listeners idempotently.
- Iframe configuration uses an exact allowed/target origin.
- No direct `UnityEngine.Debug`; use Deucarian Logging.
- Operational diagnostics contain no source URLs, tokens, or command payloads.
- Editor integration uses Build Pipeline's shared manager; do not add local chrome.
- A product definition suppresses the generic fallback scenes and must produce
  exactly one ordered Development/Production workflow.
- First-paint theme generation and required TMP readiness are shared build
  integration, never product-project build code.

## Validation

Run the Package Registry validator, EditMode and PlayMode tests on Unity 6000,
the browser harness contract tests, and `git diff --check`.
