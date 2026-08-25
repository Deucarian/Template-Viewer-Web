using System;
using Deucarian.CommandRouting.WebGLIntegration;
using NUnit.Framework;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerBrowserTransportOptionsTests
    {
        [Test]
        public void TopLevelBrowserDefaultsToDirectPage()
        {
            WebGlCommandTransportOptions options =
                WebViewerBrowserTransportOptions.Create(
                    "activity-viewer",
                    false,
                    "http://localhost:8080",
                    new EmbeddingContext(false, string.Empty));

            Assert.That(
                options.Mode,
                Is.EqualTo(WebGlCommandTransportMode.DirectPage));
            Assert.That(options.AllowedOrigins, Is.Empty);
            Assert.That(options.TargetOrigin, Is.Empty);
        }

        [Test]
        public void EmbeddedBrowserUsesDeploymentOriginInsteadOfSceneFallback()
        {
            WebGlCommandTransportOptions options =
                WebViewerBrowserTransportOptions.Create(
                    "activity-viewer",
                    false,
                    "http://localhost:8080",
                    new EmbeddingContext(
                        true,
                        "https://backoffice.example"));

            Assert.That(
                options.Mode,
                Is.EqualTo(WebGlCommandTransportMode.ParentIframe));
            Assert.That(
                options.AllowedOrigins,
                Is.EquivalentTo(new[] { "https://backoffice.example" }));
            Assert.That(
                options.TargetOrigin,
                Is.EqualTo("https://backoffice.example"));
        }

        [Test]
        public void EmbeddedBrowserFailsClosedWithoutDeploymentOrigin()
        {
            Assert.Throws<ArgumentException>(() =>
                WebViewerBrowserTransportOptions.Create(
                    "activity-viewer",
                    false,
                    "http://localhost:8080",
                    new EmbeddingContext(true, string.Empty)));
        }

        [Test]
        public void ExplicitSceneIframeRemainsAvailableForLocalHarnesses()
        {
            WebGlCommandTransportOptions options =
                WebViewerBrowserTransportOptions.Create(
                    "web-viewer",
                    true,
                    "http://localhost:8080",
                    new EmbeddingContext(false, string.Empty));

            Assert.That(
                options.Mode,
                Is.EqualTo(WebGlCommandTransportMode.ParentIframe));
            Assert.That(
                options.TargetOrigin,
                Is.EqualTo("http://localhost:8080"));
        }

        private sealed class EmbeddingContext :
            IWebViewerBrowserEmbeddingContext
        {
            private readonly bool isParentIframe;
            private readonly string parentOrigin;

            public EmbeddingContext(bool isIframe, string origin)
            {
                isParentIframe = isIframe;
                parentOrigin = origin;
            }

            public bool IsParentIframe() => isParentIframe;

            public string GetConfiguredParentOrigin() => parentOrigin;
        }
    }
}
