using System;
using Deucarian.CommandRouting.WebGLIntegration;
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
        private readonly WebViewerLifecycleStatusSink startupStatusSink =
            new WebViewerLifecycleStatusSink();
        private string compositionFailureCode = "viewer_composition_failed";

        public bool IframeMode => iframeMode;
        public string ParentOrigin => parentOrigin;
        public string TransportId => transportId;

        protected override bool EnableModelRevealReadiness => true;

        public bool TryValidateConfiguration(
            bool production,
            out string issue) =>
                WebViewerPlatformConfiguration.TryValidate(
                    iframeMode,
                    parentOrigin,
                    production,
                    out issue);

        protected override IViewerLifecycleStatusSink
            CreateEarlyLifecycleStatusSink() => startupStatusSink;

        protected override string CompositionFailureCode => compositionFailureCode;

        protected override IViewerPlatformAdapter CreatePlatformAdapter()
        {
            compositionFailureCode = "viewer_composition_failed";
            WebGlCommandTransportOptions options;
            try
            {
                options = WebViewerBrowserTransportOptions.Create(
                    transportId,
                    iframeMode,
                    parentOrigin,
                    new WebViewerBrowserEmbeddingContextInterop());
            }
            catch (InvalidOperationException)
            {
                // This factory's InvalidOperationException is its explicit
                // invalid/missing parent-origin check; never forward its text.
                compositionFailureCode = "viewer_parent_origin_invalid";
                throw;
            }

            return new WebViewerPlatformAdapter(gameObject, options, startupStatusSink);
        }

        protected override bool TryValidatePlatformConfiguration(
            IViewerPlatformAdapter adapter,
            bool production,
            out string issue) =>
                TryValidateConfiguration(production, out issue);
    }
}
