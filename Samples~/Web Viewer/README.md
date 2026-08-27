# Web Viewer sample

Open `Scenes/WebViewer.unity` and enter Play Mode. The existing
`WebViewerBootstrap` component now supplies only the browser adapter; its
inherited generic core composes the embedded sample model, reference rendering,
navigation, status shell, authentication, commands, and visibility behavior.

Use the Command Routing Live Tester in Editor or `Browser~/harness.html` with a
WebGL build. Omitting `model_url` uses the embedded sample model. Supplying a
model URL exercises the generic core's Object Loading path. Never commit real
tokens or production URLs to this sample.
