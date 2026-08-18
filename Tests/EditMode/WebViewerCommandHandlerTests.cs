using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewerWeb.Commands;
using Deucarian.ViewerAuthentication;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerCommandHandlerTests
    {
        [Test]
        public void RegistersOnlyTheDocumentedGenericApplicationCommands()
        {
            string[] names = WebViewerCommandHandlers.Create()
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "clear_access_token",
                    "clear_selection",
                    "dispose_viewer",
                    "initialize_viewer",
                    "refresh_access_token",
                    "select_elements",
                    "update_access_token",
                    "updateaccesstoken"
                },
                names);
        }

        [Test]
        public void ContainsNoReportOrActivityCommandNames()
        {
            string[] names = WebViewerCommandHandlers.Create()
                .SelectMany(handler => handler.CommandNames)
                .ToArray();

            Assert.That(names.Any(name => name.Contains("report")), Is.False);
            Assert.That(names.Any(name => name.Contains("activity")), Is.False);
        }

        [Test]
        public async Task AuthenticationOutcomesUseTheExistingSanitizedPublisher()
        {
            var publisher = new RecordingEventPublisher();
            var adapter = new WebViewerAuthenticationEventPublisher(
                publisher,
                "parent:https://host.example");
            var expiry = new DateTimeOffset(
                2026,
                8,
                18,
                10,
                30,
                0,
                TimeSpan.Zero);

            await adapter.PublishAsync(
                ViewerAuthenticationEventNames.AccessTokenUpdated,
                new ViewerAuthenticationStatusSnapshot(
                    ViewerAuthenticationStatus.Active,
                    true,
                    true,
                    expiry),
                CancellationToken.None);

            Assert.That(
                publisher.EventName,
                Is.EqualTo(ViewerAuthenticationEventNames.AccessTokenUpdated));
            Assert.That(
                publisher.RemoteEndpoint,
                Is.EqualTo("parent:https://host.example"));
            Assert.That(publisher.Payload.Value<string>("status"), Is.EqualTo("Active"));
            Assert.That(publisher.Payload.Value<bool>("has_access_token"), Is.True);
            Assert.That(publisher.Payload.Value<bool>("can_refresh"), Is.True);
            Assert.That(publisher.Payload.Value<bool>("expiry_known"), Is.True);
            Assert.That(
                publisher.Payload.Value<string>("expires_at_utc"),
                Is.EqualTo("2026-08-18T10:30:00.0000000+00:00"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "status",
                    "has_access_token",
                    "can_refresh",
                    "expiry_known",
                    "expires_at_utc"
                },
                publisher.Payload.Properties()
                    .Select(property => property.Name)
                    .ToArray());
        }

        private sealed class RecordingEventPublisher :
            IWebViewerEventPublisher
        {
            public string EventName { get; private set; }
            public JObject Payload { get; private set; }
            public string RemoteEndpoint { get; private set; }

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                EventName = eventName;
                Payload = payload;
                RemoteEndpoint = remoteEndpoint;
                return Task.CompletedTask;
            }
        }
    }
}
