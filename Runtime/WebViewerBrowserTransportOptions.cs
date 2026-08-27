using System;
using System.Runtime.InteropServices;
using Deucarian.CommandRouting.WebGLIntegration;

namespace Deucarian.TemplateViewerWeb
{
    public interface IWebViewerBrowserEmbeddingContext
    {
        bool IsParentIframe();
        string GetConfiguredParentOrigin();
    }

    internal sealed class WebViewerBrowserEmbeddingContextInterop :
        IWebViewerBrowserEmbeddingContext
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int DeucarianWebViewerIsParentIframe();

        [DllImport("__Internal")]
        private static extern string
            DeucarianWebViewerGetConfiguredParentOrigin();
#endif

        public bool IsParentIframe()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return DeucarianWebViewerIsParentIframe() == 1;
#else
            return false;
#endif
        }

        public string GetConfiguredParentOrigin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return DeucarianWebViewerGetConfiguredParentOrigin();
#else
            return string.Empty;
#endif
        }
    }

    public static class WebViewerBrowserTransportOptions
    {
        public static WebGlCommandTransportOptions Create(
            string transportId,
            bool configuredIframeMode,
            string configuredParentOrigin,
            IWebViewerBrowserEmbeddingContext embeddingContext)
        {
            bool browserIframe =
                embeddingContext?.IsParentIframe() == true;
            if (!browserIframe && !configuredIframeMode)
            {
                return new WebGlCommandTransportOptions(transportId);
            }

            // A real browser iframe may only trust deployment-owned runtime
            // configuration. It must never inherit a serialized localhost or
            // staging fallback from the Unity scene.
            string origin = browserIframe
                ? embeddingContext.GetConfiguredParentOrigin()
                : configuredParentOrigin;
            return new WebGlCommandTransportOptions(
                transportId,
                WebGlCommandTransportMode.ParentIframe,
                new[] { origin },
                origin);
        }
    }

    public static class WebViewerPlatformConfiguration
    {
        public static bool TryValidate(
            bool iframeMode,
            string parentOrigin,
            bool production,
            out string issue)
        {
            if (!iframeMode)
            {
                issue = string.Empty;
                return true;
            }

            if (!Uri.TryCreate(
                    parentOrigin,
                    UriKind.Absolute,
                    out Uri origin) ||
                (origin.Scheme != Uri.UriSchemeHttp &&
                 origin.Scheme != Uri.UriSchemeHttps) ||
                origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) ||
                !string.IsNullOrEmpty(origin.Fragment) ||
                !string.IsNullOrEmpty(origin.UserInfo))
            {
                issue =
                    "Iframe mode requires an exact HTTP(S) parent origin.";
                return false;
            }

            if (production &&
                (origin.Scheme != Uri.UriSchemeHttps || origin.IsLoopback))
            {
                issue = "Production iframe mode requires an exact " +
                        "non-loopback HTTPS origin.";
                return false;
            }

            issue = string.Empty;
            return true;
        }
    }
}
