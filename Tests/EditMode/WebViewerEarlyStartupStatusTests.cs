using System;
using System.Reflection;
using Deucarian.TemplateViewer;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerEarlyStartupStatusTests
    {
        [Test]
        public void EarlySinkExistsBeforeOriginValidationAndIsReusedByTheAdapter()
        {
            var root = new GameObject("Web early status contract");
            try
            {
                var bootstrap = root.AddComponent<WebViewerBootstrap>();
                bootstrap.enabled = false;
                IViewerLifecycleStatusSink early = GetEarlySink(bootstrap);
                Assert.That(early, Is.Not.Null);
                Assert.That(GetEarlySink(bootstrap), Is.SameAs(early));
                var adapter = (IViewerPlatformAdapter)typeof(WebViewerBootstrap).GetMethod(
                    "CreatePlatformAdapter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bootstrap, null);
                try { Assert.That(adapter.LifecycleStatusSink, Is.SameAs(early)); }
                finally { adapter.Dispose(); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("")]
        [TestCase("*")]
        [TestCase("https://host.invalid/path")]
        public void OriginFailureUsesASafeCodeBeforeApplicationComposition(string origin)
        {
            var root = new GameObject("Web invalid startup origin");
            try
            {
                var bootstrap = root.AddComponent<WebViewerBootstrap>();
                bootstrap.enabled = false;
                typeof(WebViewerBootstrap).GetField("iframeMode",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bootstrap, true);
                typeof(WebViewerBootstrap).GetField("parentOrigin",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bootstrap, origin);
                IViewerLifecycleStatusSink early = GetEarlySink(bootstrap);
                var exception = Assert.Throws<TargetInvocationException>(() =>
                    typeof(ViewerBootstrap).GetMethod("Compose",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(bootstrap.Application, Is.Null);
                Assert.That(bootstrap.PlatformAdapter, Is.Null);
                Assert.That(GetEarlySink(bootstrap), Is.SameAs(early));
                Assert.That(typeof(WebViewerBootstrap).GetProperty("CompositionFailureCode",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bootstrap),
                    Is.EqualTo("viewer_parent_origin_invalid"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void NextPlatformAttemptClearsThePreviousOriginFailureCode()
        {
            var root = new GameObject("Web startup retry status");
            try
            {
                var bootstrap = root.AddComponent<WebViewerBootstrap>();
                bootstrap.enabled = false;
                var factory = typeof(WebViewerBootstrap).GetMethod(
                    "CreatePlatformAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
                var mode = typeof(WebViewerBootstrap).GetField(
                    "iframeMode", BindingFlags.Instance | BindingFlags.NonPublic);
                var code = typeof(WebViewerBootstrap).GetProperty(
                    "CompositionFailureCode", BindingFlags.Instance | BindingFlags.NonPublic);
                mode.SetValue(bootstrap, true);
                typeof(WebViewerBootstrap).GetField("parentOrigin",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bootstrap, "*");

                Assert.Throws<TargetInvocationException>(() => factory.Invoke(bootstrap, null));
                Assert.That(code.GetValue(bootstrap), Is.EqualTo("viewer_parent_origin_invalid"));

                mode.SetValue(bootstrap, false);
                var adapter = (IViewerPlatformAdapter)factory.Invoke(bootstrap, null);
                try
                {
                    Assert.That(code.GetValue(bootstrap), Is.EqualTo("viewer_composition_failed"));
                }
                finally { adapter.Dispose(); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static IViewerLifecycleStatusSink GetEarlySink(WebViewerBootstrap bootstrap) =>
            (IViewerLifecycleStatusSink)typeof(ViewerBootstrap).GetProperty(
                "EarlyLifecycleStatusSink", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(bootstrap);
    }
}
