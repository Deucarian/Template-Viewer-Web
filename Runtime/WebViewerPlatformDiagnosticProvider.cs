using System.Threading;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.Diagnostics;

namespace Deucarian.TemplateViewerWeb
{
    internal sealed class WebViewerPlatformDiagnosticProvider :
        IDiagnosticProvider
    {
        private readonly WebGlCommandTransportOptions options;
        private int active;

        internal WebViewerPlatformDiagnosticProvider(
            WebGlCommandTransportOptions transportOptions)
        {
            options = transportOptions;
        }

        public string ProviderId => "template-viewer-web-platform";
        public string DisplayName => "Web Viewer Platform";

        internal void SetActive(bool value) =>
            Interlocked.Exchange(ref active, value ? 1 : 0);

        public void Collect(DiagnosticReportBuilder builder)
        {
            bool isActive =
                Interlocked.CompareExchange(ref active, 0, 0) == 1;
            DiagnosticSection section = builder.AddSection(
                ProviderId,
                DisplayName);
            section.AddItem(
                "status",
                "Status",
                isActive ? "Active" : "Stopped",
                isActive
                    ? DiagnosticSeverity.Success
                    : DiagnosticSeverity.Info);
            section.AddItem(
                "platform",
                "Platform",
                "WebGL");
            section.AddItem(
                "mode",
                "Embedding mode",
                options.Mode.ToString());
            section.AddItem(
                "allowed_origin_count",
                "Allowed origins",
                options.AllowedOrigins.Count.ToString());
        }
    }
}
