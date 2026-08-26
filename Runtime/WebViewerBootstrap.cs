using Deucarian.TemplateViewer;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    /// <summary>
    /// WebGL compatibility bootstrap. Shared viewer composition lives in the
    /// platform-neutral Template Viewer package; this component contributes
    /// only the secured browser boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WebViewerBootstrap : ViewerBootstrap
    {
        [Header("Browser transport")]
        [SerializeField] private bool iframeMode;
        [SerializeField] private string parentOrigin =
            "http://localhost:8080";
        [SerializeField] private string transportId = "web-viewer";

        public bool IframeMode => iframeMode;
        public string ParentOrigin => parentOrigin;
        public string TransportId => transportId;

        public bool TryValidateConfiguration(
            bool production,
            out string issue) =>
                WebViewerPlatformConfiguration.TryValidate(
                    iframeMode,
                    parentOrigin,
                    production,
                    out issue);

        protected override IViewerPlatformAdapter CreatePlatformAdapter() =>
            new WebViewerPlatformAdapter(
                gameObject,
                WebViewerBrowserTransportOptions.Create(
                    transportId,
                    iframeMode,
                    parentOrigin,
                    new WebViewerBrowserEmbeddingContextInterop()));

        protected override bool TryValidatePlatformConfiguration(
            IViewerPlatformAdapter adapter,
            bool production,
            out string issue) =>
                TryValidateConfiguration(production, out issue);
    }
}
