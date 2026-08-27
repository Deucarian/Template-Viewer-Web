using Deucarian.TemplateViewer;
using Deucarian.WebGLTemplate;

namespace Deucarian.TemplateViewerWeb
{
    /// <summary>
    /// Projects platform-neutral lifecycle state into the WebGL page shell.
    /// The in-viewer shell remains owned by the generic Template Viewer core.
    /// </summary>
    internal sealed class WebViewerLifecycleStatusSink :
        IViewerLifecycleStatusSink
    {
        public void ReportLifecycle(
            ViewerLifecycleState lifecycle,
            string message)
        {
            switch (lifecycle)
            {
                case ViewerLifecycleState.Created:
                case ViewerLifecycleState.Loading:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Loading,
                        message);
                    break;
                case ViewerLifecycleState.Ready:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Ready,
                        message);
                    break;
                case ViewerLifecycleState.Failed:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Failed,
                        message);
                    break;
                case ViewerLifecycleState.Disposed:
                    DeucarianWebGLShell.ReportState(
                        DeucarianWebGLShellState.Disposed,
                        message);
                    break;
            }
        }

        public void ReportLoadingProgress(
            string operationId,
            float normalized,
            string message) =>
                DeucarianWebGLShell.ReportProgress(
                    operationId,
                    normalized,
                    message);
    }
}
