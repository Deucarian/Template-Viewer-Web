using System;
using Deucarian.ViewerShell;
using Deucarian.WebGLTemplate;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    /// <summary>
    /// Projects the generic web-viewer lifecycle into the reusable viewer
    /// shell. All visual structure and behavior remain package-owned.
    /// </summary>
    internal sealed class WebViewerShellStatusAdapter : IDisposable
    {
        private readonly WebViewerApplication application;
        private readonly ViewerShellPresenter shell;
        private bool disposed;

        public WebViewerShellStatusAdapter(
            WebViewerApplication application,
            ViewerShellPresenter shell)
        {
            this.application = application ??
                throw new ArgumentNullException(nameof(application));
            this.shell = shell ??
                throw new ArgumentNullException(nameof(shell));
            application.LifecycleChanged += OnLifecycleChanged;
            application.LoadingProgressChanged += OnLoadingProgressChanged;
            OnLifecycleChanged(application.Lifecycle);
        }

        internal ViewerShellStatusSnapshot LastSnapshot { get; private set; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            application.LifecycleChanged -= OnLifecycleChanged;
            application.LoadingProgressChanged -= OnLoadingProgressChanged;
            disposed = true;
        }

        private void OnLifecycleChanged(WebViewerLifecycleState lifecycle)
        {
            string diagnostics = FormatDiagnostics();
            switch (lifecycle)
            {
                case WebViewerLifecycleState.Created:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Loading,
                        "Waiting for viewer initialization");
                    Apply(ViewerShellStatusSnapshot.Uninitialized(
                        "Waiting for viewer initialization",
                        diagnostics));
                    break;
                case WebViewerLifecycleState.Loading:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Loading,
                        "Loading model");
                    Apply(ViewerShellStatusSnapshot.Loading(
                        "Loading model\u2026",
                        diagnostics));
                    break;
                case WebViewerLifecycleState.Ready:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Ready,
                        "Viewer ready");
                    Apply(ViewerShellStatusSnapshot.Ready(
                        "Ready \u2022 " + application.IndexedElementCount +
                        " elements",
                        diagnostics));
                    break;
                case WebViewerLifecycleState.Failed:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Failed,
                        "Viewer initialization failed");
                    Apply(ViewerShellStatusSnapshot.Error(
                        "Viewer initialization failed",
                        diagnostics));
                    break;
                case WebViewerLifecycleState.Disposed:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Disposed,
                        "Viewer disposed");
                    Apply(ViewerShellStatusSnapshot.Uninitialized(
                        "Viewer disposed",
                        diagnostics));
                    break;
            }
        }

        private void OnLoadingProgressChanged(float normalized, string message)
        {
            string label = string.IsNullOrWhiteSpace(message)
                ? "Loading model"
                : message.Trim();
            Apply(ViewerShellStatusSnapshot.Loading(
                label + " \u2022 " +
                Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f) + "%",
                FormatDiagnostics()));
            DeucarianWebGLShell.ReportProgress(
                "model",
                normalized,
                label);
        }

        private void Apply(ViewerShellStatusSnapshot snapshot)
        {
            LastSnapshot = snapshot;
            shell.ApplyStatus(snapshot);
        }

        private string FormatDiagnostics()
        {
            string revision = application.LatestRevision >= 0
                ? application.LatestRevision.ToString()
                : "none";
            return "revision=" + revision +
                   "    elements=" + application.IndexedElementCount +
                   "    selected=" + application.SelectedElementCount;
        }
    }
}
