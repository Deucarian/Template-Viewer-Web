using System;
using System.Collections.Generic;
using Deucarian.TemplateViewer;
using Deucarian.TemplateViewerWeb.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerBuildValidationTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = roots.Count - 1; index >= 0; index--)
            {
                if (roots[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(roots[index]);
                }
            }

            roots.Clear();
        }

        [Test]
        public void AcceptsExactlyOneWebBootstrap()
        {
            WebViewerBootstrap bootstrap = Create<WebViewerBootstrap>();

            IReadOnlyList<string> issues =
                WebViewerBuildManagerProvider.ValidateViewerComposition(
                    new ViewerBootstrap[] { bootstrap },
                    production: true);

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void RejectsSceneWithoutAViewerBootstrap()
        {
            IReadOnlyList<string> issues =
                WebViewerBuildManagerProvider.ValidateViewerComposition(
                    Array.Empty<ViewerBootstrap>(),
                    production: true);

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0], Does.Contain("no ViewerBootstrap"));
        }

        [Test]
        public void RejectsMultipleWebBootstraps()
        {
            WebViewerBootstrap first = Create<WebViewerBootstrap>();
            WebViewerBootstrap second = Create<WebViewerBootstrap>();

            IReadOnlyList<string> issues =
                WebViewerBuildManagerProvider.ValidateViewerComposition(
                    new ViewerBootstrap[] { first, second },
                    production: true);

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0], Does.Contain("exactly one"));
        }

        [Test]
        public void RejectsMixedPlatformBootstraps()
        {
            WebViewerBootstrap web = Create<WebViewerBootstrap>();
            NonWebViewerBootstrap nonWeb = Create<NonWebViewerBootstrap>();

            IReadOnlyList<string> issues =
                WebViewerBuildManagerProvider.ValidateViewerComposition(
                    new ViewerBootstrap[] { web, nonWeb },
                    production: true);

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0], Does.Contain("exactly one"));
        }

        [Test]
        public void RejectsNonWebBootstrapAsOnlyAdapter()
        {
            NonWebViewerBootstrap bootstrap =
                Create<NonWebViewerBootstrap>();

            IReadOnlyList<string> issues =
                WebViewerBuildManagerProvider.ValidateViewerComposition(
                    new ViewerBootstrap[] { bootstrap },
                    production: true);

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0], Does.Contain("requires WebViewerBootstrap"));
        }

        private T Create<T>() where T : Component
        {
            var root = new GameObject(typeof(T).Name);
            roots.Add(root);
            return root.AddComponent<T>();
        }

        public sealed class NonWebViewerBootstrap : ViewerBootstrap
        {
            protected override IViewerPlatformAdapter
                CreatePlatformAdapter() => null;
        }
    }
}
