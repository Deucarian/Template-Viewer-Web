# Changelog

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
