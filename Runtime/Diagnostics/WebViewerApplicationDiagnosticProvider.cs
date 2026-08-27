using Deucarian.Diagnostics;
using Deucarian.Authentication;

namespace Deucarian.TemplateViewerWeb.Diagnostics
{
    public sealed class WebViewerApplicationDiagnosticProvider : IDiagnosticProvider
    {
        private readonly WebViewerApplication application;

        public WebViewerApplicationDiagnosticProvider(WebViewerApplication viewerApplication)
        {
            application = viewerApplication;
        }

        public string ProviderId => "template-viewer-web";
        public string DisplayName => "Web Viewer Template";

        public void Collect(DiagnosticReportBuilder builder)
        {
            WebViewerLifecycleState lifecycle = application?.Lifecycle ??
                WebViewerLifecycleState.Disposed;
            DiagnosticSeverity severity = lifecycle == WebViewerLifecycleState.Failed
                ? DiagnosticSeverity.Error
                : lifecycle == WebViewerLifecycleState.Ready
                    ? DiagnosticSeverity.Success
                    : DiagnosticSeverity.Info;
            DiagnosticSection section = builder.AddSection(ProviderId, DisplayName);
            section.AddItem(
                "lifecycle",
                "Lifecycle",
                lifecycle.ToString(),
                severity);
            section.AddItem(
                "latest_revision",
                "Latest revision",
                (application?.LatestRevision ?? -1).ToString());
            section.AddItem(
                "indexed_elements",
                "Indexed elements",
                (application?.IndexedElementCount ?? 0).ToString());
            section.AddItem(
                "selected_elements",
                "Selected elements",
                (application?.SelectedElementCount ?? 0).ToString());
            AuthenticationStatusSnapshot authentication =
                application?.AuthenticationSession?.Status ??
                new AuthenticationStatusSnapshot(
                    AuthenticationStatus.Missing,
                    false,
                    false,
                    null);
            section.AddItem(
                "authentication",
                "Authentication",
                authentication.Status.ToString(),
                GetAuthenticationSeverity(authentication.Status));
        }

        private static DiagnosticSeverity GetAuthenticationSeverity(
            AuthenticationStatus status)
        {
            switch (status)
            {
                case AuthenticationStatus.Active:
                    return DiagnosticSeverity.Success;
                case AuthenticationStatus.Expired:
                    return DiagnosticSeverity.Error;
                case AuthenticationStatus.Expiring:
                case AuthenticationStatus.ExpiryUnknown:
                case AuthenticationStatus.Missing:
                default:
                    return DiagnosticSeverity.Warning;
            }
        }
    }
}
