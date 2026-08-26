# Changelog

## [0.3.7] - 2026-08-26

### Added

- Let product initialization apply a runtime-only model placement and opt into
  shared renderer-bounds centering before visibility and navigation compose.

### Changed

- Use the stable loaded-model parent as the reference root so placement and
  centering preserve every AssetBundle-authored child transform.
- Include inactive model renderers in origin preparation without changing any
  object visibility.

## [0.3.6] - 2026-08-25

### Added

- Resolve secure parent-iframe mode from deployment-owned browser
  configuration while preserving direct-page localhost and Editor workflows.
- Let one product feature replace the generic `initialize_viewer` handler and
  publish product lifecycle events through the viewer's secured transport.

### Security

- Embedded builds fail closed when `window.deucarianWebViewerConfig` does not
  contain one exact HTTP(S) `parentOrigin`; serialized localhost origins are
  never used as an iframe fallback.

## [0.3.5] - 2026-08-25

### Fixed

- Transition the viewer lifecycle to Failed when initialization or lifecycle
  event publication throws, even when the failure event cannot be delivered.
- Require Viewer Authentication 0.5.1 so standalone template installations
  retain authenticated Editor sessions through Play Mode transitions.

## [0.3.4] - 2026-08-25

### Fixed

- Describe the pre-command state as waiting for viewer initialization instead
  of incorrectly implying that local Editor Play Mode requires a browser host.

## [0.3.3] - 2026-08-25

### Fixed

- Defer viewer composition for one frame so Editor Play Mode connection
  providers can register the selected project/API context before authentication
  and model loading are composed.

## [0.3.2] - 2026-08-25

### Added

- Let product features nominate the command example that the shared Unity
  live tester opens by default, without changing automated sequence order.

## [0.3.1] - 2026-08-25

### Fixed

- Route Editor test commands through the direct-page or parent-iframe endpoint
  expected by the active Web Viewer transport.

## [0.3.0] - 2026-08-24

### Added

- Registered the live Web Viewer command composition with Command Routing's
  Unity Editor tester.
- Reused the generated browser scenarios for individual Editor dispatch and
  ordered automatic command sequences.

## [0.2.1] - 2026-08-24

### Added

- Added a loopback-only iframe harness with a mock Unity viewer, dynamic
  generated command controls, exact response matching, and a one-click
  automated scenario run.
- Added Unity editor catalog generation from the actual bootstrap command
  composition, including product feature examples and safe non-automated
  fallbacks for commands without representative payloads.

### Changed

- Replaced the handwritten browser action list with the generated catalog so
  new Unity commands cannot silently disappear from local iframe testing.

## [0.2.0] - 2026-08-24

### Added

- Added model lifecycle notifications and scene-local product feature components.
- Added extra-command composition and one replaceable visibility owner.
- Adopted the reusable Deucarian WebGL browser template and lifecycle bridge.

## [0.1.20] - 2026-08-24

### Changed

- Installs Theming's complete shared viewer reference runtime so generated
  viewers use the same theme family, persisted light/dark mode, canonical web
  snapshot, and provider ownership as the Report Viewer reference consumer.
- Updated Theming to 1.1.0 and added whole-composition parity coverage.
- Declared the EditMode tests' direct Session assembly dependency and kept
  Unity object cleanup unambiguous with the current API dependency set.

## [0.1.19] - 2026-08-19

### Changed

- Replaced the template-local model authentication rule with Object Loading API
  Integration's shared trusted-origin policy.
- Canonicalizes relative model endpoints against the active connection's API
  base before loading and uses optional live-provider authentication for trusted
  relative, same-origin, and explicitly allowlisted destinations.
- Preserves anonymous public loading for untrusted absolute HTTP(S) model URLs,
  including when the viewer has no configured API base.
- Updated Object Loading API Integration to 0.2.8.

## [0.1.18] - 2026-08-19

### Added

- Consumes the optional vendor-neutral Viewer Authentication runtime connection
  composition when exactly one provider resolves, sharing its stable target,
  session, API client, base URL, and authenticated model origins.

### Changed

- Keeps the template-owned authentication target only as the no-provider
  fallback. Failed or ambiguous optional connection providers now stop viewer
  initialization instead of silently falling back to a different session.
- Updated Viewer Authentication to 0.5.0 for runtime connection composition.

## [0.1.17] - 2026-08-19

### Added

- Exposed the composed viewer command runtime through Command Routing's
  injected scene ingress, allowing optional development connections to submit
  the canonical `initialize_viewer` envelope without template internals.

### Changed

- Updated the generic API and Command Routing dependencies for environment
  catalogs and local command ingress.

## [0.1.16] - 2026-08-19

- Updated Viewer Authentication to 0.4.0 and Web Viewer Suite to 0.1.14 so
  generated viewers inherit the same minimalist connection workspace, exact
  viewer-bound token storage, and stale-operation safeguards as the reference
  viewers.

## [0.1.15] - 2026-08-19

- Updated Viewer Authentication to 0.3.1 and Web Viewer Suite to 0.1.13 so
  generated viewers clearly show developers which authentication endpoint and
  environment the shared authentication menu targets.

## [0.1.14] - 2026-08-18

- Updated Session API Integration to 1.1.1, Viewer Authentication to 0.3.0,
  and Web Viewer Suite to 0.1.12 so generated viewers receive the shared Edit
  Mode authentication workspace and optional server-side token validation.

## [0.1.13] - 2026-08-18

- Composes the shared interactive token-endpoint provider from an assigned or
  conventional Resources profile, so generated viewers receive the same
  package-owned Refresh Token workflow without local authentication scripts.
- Updated API, Session API Integration, Viewer Authentication, and Web Viewer
  Suite dependencies for credential-safe endpoint acquisition.

## [0.1.12] - 2026-08-18

- Declares the shared Session package and runtime assembly directly so the
  template's authentication composition compiles cleanly in consumers.

## [0.1.11] - 2026-08-18

- Composed the shared Viewer Authentication session, commands, and development
  tooling so generated viewers inherit the same live token workflow.
- Published authentication command outcomes through the template's existing
  browser event channel using token-free lifecycle snapshots.
- Routed trusted model downloads through the live API authentication provider
  without copying bearer tokens into model descriptors or diagnostics.

## [0.1.10] - 2026-08-18

- Replaced the template-local status presentation with the reusable reference
  viewer shell, including Report-donor status/toast, information and display
  settings menus, responsive chrome, tooltips, input boundaries, and layering.
- Composed the reusable reference rendering baseline so camera, key light,
  environment, post-processing, and display policy match every viewer consumer.
- Uses Viewer Rendering's package-owned Full quality tier on desktop and WebGL,
  so consumer project quality indices cannot change the reference appearance.
- Injected one authoritative reference theme provider through rendering,
  navigation, and shell, and added whole-composition parity coverage.

## [0.1.9] - 2026-08-18

- Updated to UI 0.2.6 and Viewer Navigation 0.1.9 so all runtime viewer
  surfaces use the shared semantic depth contract and canonical PanelSettings.
- Replaced the template's legacy uGUI status canvas with a canonical UI Toolkit
  document on the UI-owned `Status` surface role. Navigation remains on
  `PrimaryControls`, while the package-owned tooltip panel is guaranteed to
  compose above both documents.
- Preserved the status panel's bottom-left 300 x 42 layout, lifecycle copy,
  theme-derived colors, typography, and Frosted Glass presentation without a
  direct uGUI dependency.
- Updated the Web Viewer Suite dependency to 0.1.8 and added composition-level
  parity coverage for canonical panel ownership, semantic depth, and tooltip
  topness.

## [0.1.8] - 2026-08-17

- Updated to Viewer Navigation 0.1.7 so the template composes the canonical
  package-owned toolbar assets, chrome, pointer behavior, and input suppression
  instead of carrying a consumer-specific presentation fork.
- Updated to Theming 1.0.5 for the shared UI Toolkit theme and typography
  adapter used by the reference navigation presentation.
- Updated the Web Viewer Suite dependency to 0.1.7 and added parity coverage
  for the installed toolbar element tree and resolved reference theme.

## [0.1.7] - 2026-08-17

- Updated to the package-owned WebGL reduced-motion policy in Viewer Navigation
  0.1.6 and asserted that the template uses that shared default rather than a
  consumer-specific animation gate.
- Updated the Web Viewer Suite dependency to 0.1.6.

## [0.1.6] - 2026-08-17

- Updated to Viewer Navigation 0.1.5, Web Viewer Suite 0.1.5, and Theming
  1.0.4.
- Cached and exposed the template's effective reference navigation composition,
  including the shared preset, UI input blocker, mesh-bounds strategy, and
  runtime-only animation policy, reference theme profile, and default dark mode.
- Custom navigation settings now use the composition's `WithPreset` API so all
  shared policies and theme object identities remain intact.
- Installed and exposed the composition's theme provider and effective
  `CurrentTheme`; the status overlay now resolves its surface, text, and error
  roles from that theme and applies the shared Frosted Glass chrome style.
- Added parity coverage for resolved settings and theme identities, EditMode
  animation behavior, the installed controller/provider, and themed overlay.

## [0.1.5] - 2026-08-17

- Updated to Viewer Navigation 0.1.4 and Web Viewer Suite 0.1.4 so newly
  imported viewers receive the polished Report Viewer icon toolbar,
  theme-driven interactions, and runtime tooltips from the shared package.
- Kept the optional view cube disabled by default.

## [0.1.4] - 2026-08-17

- Updated to Viewer Navigation 0.1.3 and Web Viewer Suite 0.1.3 so fresh
  template installs keep the optional view cube hidden by default.

## [0.1.3] - 2026-08-14

- Removed the unused direct Common dependency so the template contract matches
  its actual assembly usage and passes the authoritative dependency audit.

## [0.1.2] - 2026-08-14

- Made the sample composition resolve Viewer Navigation's canonical reference preset,
  giving new viewer projects the same controls, transition timing, framing tuning,
  toolbar, and view-cube defaults as Report Viewer.
- Added parity coverage so the generic template cannot silently fall back to a
  different navigation configuration.

## [0.1.1] - 2026-08-14

- Replaced the provisional browser host with the canonical WebGL Integration 0.1.1 distributable.
- Updated the harness to the supported listener and `sendCommand` API and added executable lifecycle coverage.
- Aligned the template with the two-consumer-proven shared package versions.
- Declared Camera Navigation directly because the template composition consumes types exposed by Viewer Navigation's public installer API.

## [0.1.0] - 2026-08-13

- Added the generic Web Viewer template runtime and explicit composition root.
- Added secure WebGL commands, revisioned visibility, and sanitized diagnostics.
- Added a local browser harness, runnable sample, and Build Pipeline provider.
- Reserved accepted initialization revisions before asynchronous work and added
  generation guards so stale in-flight initialization cannot regain ownership.
- Made the local browser harness self-contained outside the source checkout.
