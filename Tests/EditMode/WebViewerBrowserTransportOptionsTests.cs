using System;
using System.Reflection;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.TemplateViewer;
using NUnit.Framework;
using UnityEngine;

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
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                WebViewerBrowserTransportOptions.Create(
                    "activity-viewer",
                    false,
                    "http://localhost:8080",
                    new EmbeddingContext(true, string.Empty)));

            Assert.That(
                exception.Message,
                Does.Contain("Configure the host page"));
            Assert.That(exception.Message, Does.Not.Contain("localhost"));
        }

        [Test]
        public void ExplicitSceneIframeExplainsHowToFixMissingOrigin()
        {
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    WebViewerBrowserTransportOptions.Create(
                        "report-viewer",
                        true,
                        string.Empty,
                        new EmbeddingContext(false, string.Empty)));

            Assert.That(
                exception.Message,
                Does.Contain("Web Viewer Bootstrap"));
            Assert.That(exception.Message, Does.Contain("Iframe Mode"));
            Assert.That(exception.Message, Does.Contain("Parent Origin"));
            Assert.That(
                exception.Message,
                Does.Contain("disable Iframe Mode"));
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

        [Test]
        public void ProductionIframeRequiresSecureNonLoopbackOrigin()
        {
            Assert.That(
                WebViewerPlatformConfiguration.TryValidate(
                    true,
                    "http://localhost:8080",
                    true,
                    out string issue),
                Is.False);
            Assert.That(issue, Does.Contain("non-loopback HTTPS"));

            Assert.That(
                WebViewerPlatformConfiguration.TryValidate(
                    true,
                    "https://backoffice.example",
                    true,
                    out issue),
                Is.True,
                issue);
        }

        [Test]
        public void BootstrapIsAThinGenericViewerHost()
        {
            Assert.That(
                typeof(WebViewerBootstrap).BaseType,
                Is.EqualTo(typeof(ViewerBootstrap)));
        }

        [Test]
        public void BootstrapOptsIntoTheSharedModelRevealReadiness()
        {
            var root = new GameObject("Web reveal readiness contract");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                PropertyInfo property = typeof(ViewerBootstrap).GetProperty(
                    "EnableModelRevealReadiness",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(property, Is.Not.Null);
                Assert.That(property.GetValue(bootstrap), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
