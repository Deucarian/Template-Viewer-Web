# Browser protocol

Command envelopes and viewer command/event payloads are owned by Command
Routing and the platform-neutral Template Viewer core. This adapter preserves
their wire format and adds no product-specific DTOs.

Direct-page hosts use the `direct` endpoint. Iframe hosts use
`parent:<exact-origin>`, where the exact origin is supplied through
`window.deucarianWebViewerConfig.parentOrigin` before Unity starts. The same
endpoint is used for inbound commands, command responses, application events,
and authentication lifecycle events.

Transport readiness means that the browser listener is installed. Hosts must
still wait for the core application's `viewer_ready` event before assuming the
model, identifier index, reference navigation, and command composition are
ready. Product packages may publish additional readiness events after that
point when their own metadata is asynchronous.

An invalid or absent deployment origin in an actual iframe fails closed. The
adapter validates the parent source window and never uses wildcard origins.
