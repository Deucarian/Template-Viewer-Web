using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.TemplateViewerWeb.Commands;
using Deucarian.TemplateViewerWeb.Loading;
using Deucarian.ViewerNavigation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerDirectPageLifecycleRoutingTests
    {
        [Test]
        public async Task EditorLocalDirectRoutePublishesLifecycleEvents()
        {
            var browser = new RecordingBrowserInterop();
            using (var transport = CreateStartedDirectTransport(browser))
            {
                RoutedInitialization result = await RouteInitializationAsync(
                    transport,
                    "direct");

                Assert.That(result.Outcome.Result.ErrorCode,
                    Is.EqualTo("initialization_failed"));
                Assert.That(result.Lifecycle, Is.EqualTo(WebViewerLifecycleState.Failed));
                Assert.That(browser.Events, Has.Count.EqualTo(2));
                Assert.That(browser.Events[0].Name, Is.EqualTo("viewer_loading"));
                Assert.That(browser.Events[1].Name, Is.EqualTo("viewer_failed"));
                Assert.That(browser.Events[0].RemoteEndpoint, Is.EqualTo("direct"));
                Assert.That(browser.Events[1].RemoteEndpoint, Is.EqualTo("direct"));
            }
        }

        [Test]
        public async Task DirectRouteRejectsNonDirectLifecycleEndpoint()
        {
            var browser = new RecordingBrowserInterop();
            using (var transport = CreateStartedDirectTransport(browser))
            {
                RoutedInitialization result = await RouteInitializationAsync(
                    transport,
                    "development-profile");

                Assert.That(result.Outcome.Result.ErrorCode,
                    Is.EqualTo("initialization_failed"));
                Assert.That(result.Lifecycle, Is.EqualTo(WebViewerLifecycleState.Failed));
                Assert.That(browser.Events, Is.Empty);
            }
        }

        [Test]
        public void IframePublisherRequiresTheConfiguredParentEndpoint()
        {
            const string parentOrigin = "https://backoffice.example";
            var browser = new RecordingBrowserInterop();
            using (var transport = new WebGlCommandTransport(
                       new WebGlCommandTransportOptions(
                           "iframe-viewer",
                           WebGlCommandTransportMode.ParentIframe,
                           new[] { parentOrigin },
                           parentOrigin),
                       browser))
            {
                transport.Start();
                var publisher = new WebGlWebViewerEventPublisher(transport);

                Assert.Throws<InvalidOperationException>(() =>
                    publisher.PublishAsync(
                            "viewer_loading",
                            new JObject(),
                            "direct",
                            CancellationToken.None)
                        .GetAwaiter().GetResult());

                Assert.DoesNotThrow(() =>
                    publisher.PublishAsync(
                            "viewer_loading",
                            new JObject(),
                            "parent:" + parentOrigin,
                            CancellationToken.None)
                        .GetAwaiter().GetResult());
            }

            Assert.That(browser.Events, Has.Count.EqualTo(1));
            Assert.That(browser.Events[0].RemoteEndpoint,
                Is.EqualTo("parent:" + parentOrigin));
        }

        private static WebGlCommandTransport CreateStartedDirectTransport(
            RecordingBrowserInterop browser)
        {
            var transport = new WebGlCommandTransport(
                new WebGlCommandTransportOptions(
                    "editor-local-viewer",
                    WebGlCommandTransportMode.DirectPage),
                browser);
            transport.Start();
            return transport;
        }

        private static async Task<RoutedInitialization> RouteInitializationAsync(
            WebGlCommandTransport transport,
            string remoteEndpoint)
        {
            GameObject model = new GameObject("Embedded model");
            GameObject navigationObject = new GameObject("Navigation");
            GameObject routeObject = new GameObject("Command route");
            try
            {
                using (var application = new WebViewerApplication(
                           new DirectWebViewerModelDescriptorResolver(),
                           new EmbeddedOnlyModelLoader(),
                           navigationObject.AddComponent<ViewerNavigationInstaller>(),
                           new WebGlWebViewerEventPublisher(transport),
                           model,
                           customVisibilityFeatureFactory:
                               new RejectingVisibilityFeatureFactory()))
                using (var runtime = new CommandRoutingRuntime<WebViewerApplication>(
                           application,
                           WebViewerCommandHandlers.Create()))
                {
                    CommandRoutePortBehaviour port =
                        routeObject.AddComponent<CommandRoutePortBehaviour>();
                    port.Initialize(runtime);

                    CommandRouteOutcome outcome = await port.RouteMessageAsync(
                        CreateInitializationJson(),
                        "editor-local",
                        remoteEndpoint,
                        CancellationToken.None);
                    return new RoutedInitialization(outcome, application.Lifecycle);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeObject);
                UnityEngine.Object.DestroyImmediate(navigationObject);
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        private static string CreateInitializationJson() =>
            new JObject
            {
                ["protocol_version"] = 1,
                ["command_id"] = "editor-local-initialize",
                ["command"] = "initialize_viewer",
                ["payload"] = new JObject { ["revision"] = 7L },
                ["metadata"] = new JObject
                {
                    ["source"] = "simultria-development-profile",
                    ["transport"] = "editor-local",
                    ["remote_endpoint"] = "direct"
                }
            }.ToString();

        private readonly struct RoutedInitialization
        {
            public RoutedInitialization(
                CommandRouteOutcome outcome,
                WebViewerLifecycleState lifecycle)
            {
                Outcome = outcome;
                Lifecycle = lifecycle;
            }

            public CommandRouteOutcome Outcome { get; }
            public WebViewerLifecycleState Lifecycle { get; }
        }

        private sealed class RejectingVisibilityFeatureFactory :
            IWebViewerVisibilityFeatureFactory
        {
            public bool TryCreate(
                WebViewerModelContext context,
                out IWebViewerVisibilityFeature feature,
                out string error)
            {
                feature = null;
                error = "Intentional test initialization failure.";
                return false;
            }
        }

        private sealed class EmbeddedOnlyModelLoader : IWebViewerModelLoader
        {
            public Task<WebViewerModelLoadResult> LoadAsync(
                WebViewerModelDescriptor descriptor,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException(
                    "Embedded initialization must not invoke the model loader.");

            public void Unload()
            {
            }

            public void Dispose()
            {
            }
        }

        private readonly struct PublishedEvent
        {
            public PublishedEvent(string name, string remoteEndpoint)
            {
                Name = name;
                RemoteEndpoint = remoteEndpoint;
            }

            public string Name { get; }
            public string RemoteEndpoint { get; }
        }

        private sealed class RecordingBrowserInterop : IWebGlCommandBrowserInterop
        {
            public List<PublishedEvent> Events { get; } =
                new List<PublishedEvent>();

            public void Install(string configurationJson)
            {
            }

            public void Uninstall(string transportId)
            {
            }

            public void Send(
                string transportId,
                string message,
                string remoteEndpoint)
            {
            }

            public void SendEvent(
                string transportId,
                string eventName,
                string payloadJson,
                string remoteEndpoint)
            {
                Events.Add(new PublishedEvent(eventName, remoteEndpoint));
            }

            public void NotifyReady(string transportId)
            {
            }
        }
    }
}
