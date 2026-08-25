using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewerWeb.Loading;
using Deucarian.ViewerNavigation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerApplicationInitializationTests
    {
        private GameObject embeddedModel;
        private GameObject navigationObject;
        private FirstLoadingGatePublisher publisher;
        private WebViewerApplication application;

        [SetUp]
        public void SetUp()
        {
            embeddedModel = new GameObject("EmbeddedReference");
            navigationObject = new GameObject("Navigation");
            publisher = new FirstLoadingGatePublisher();
            application = new WebViewerApplication(
                new DirectWebViewerModelDescriptorResolver(),
                new EmbeddedOnlyModelLoader(),
                navigationObject.AddComponent<ViewerNavigationInstaller>(),
                publisher,
                embeddedModel);
        }

        [TearDown]
        public void TearDown()
        {
            application?.Dispose();
            publisher?.ReleaseFirstLoading();
            if (navigationObject != null)
            {
                UnityEngine.Object.DestroyImmediate(navigationObject);
            }

            if (embeddedModel != null)
            {
                UnityEngine.Object.DestroyImmediate(embeddedModel);
            }
        }

        [Test]
        public async Task OlderRevisionCannotCancelAcceptedInitialization()
        {
            Task<CommandOperationResult> accepted = InitializeAsync(10);
            Assert.That(accepted.IsCompleted, Is.False);

            CommandOperationResult stale = await InitializeAsync(9);

            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.ErrorCode, Is.EqualTo("stale_revision"));
            Assert.That(application.LatestRevision, Is.EqualTo(10));

            publisher.ReleaseFirstLoading();
            CommandOperationResult acceptedResult = await accepted;

            Assert.That(acceptedResult.ErrorCode, Is.EqualTo("initialization_failed"));
            Assert.That(application.LatestRevision, Is.EqualTo(10));
        }

        [Test]
        public async Task NewerRevisionSupersedesEmbeddedInitializationAfterLoadingEvent()
        {
            Task<CommandOperationResult> superseded = InitializeAsync(10);
            Assert.That(superseded.IsCompleted, Is.False);

            CommandOperationResult newer = await InitializeAsync(11);
            Assert.That(newer.ErrorCode, Is.EqualTo("initialization_failed"));
            Assert.That(application.LatestRevision, Is.EqualTo(11));
            Assert.That(embeddedModel.activeSelf, Is.False);

            publisher.ReleaseFirstLoading();
            CommandOperationResult supersededResult = await superseded;

            Assert.That(supersededResult.Succeeded, Is.False);
            Assert.That(supersededResult.ErrorCode, Is.EqualTo("superseded"));
            Assert.That(application.LatestRevision, Is.EqualTo(11));
            Assert.That(embeddedModel.activeSelf, Is.False);
        }

        [Test]
        public async Task EventPublishingFailureTransitionsLifecycleToFailed()
        {
            publisher.ThrowOnLoading = true;
            publisher.ThrowOnFailure = true;

            CommandOperationResult result = await InitializeAsync(12);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("initialization_failed"));
            Assert.That(application.Lifecycle, Is.EqualTo(WebViewerLifecycleState.Failed));
            Assert.That(embeddedModel.activeSelf, Is.False);
        }

        private Task<CommandOperationResult> InitializeAsync(long revision) =>
            application.InitializeAsync(
                new WebViewerInitializeRequest { Revision = revision },
                "test-host",
                CancellationToken.None);

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

        private sealed class FirstLoadingGatePublisher : IWebViewerEventPublisher
        {
            private readonly TaskCompletionSource<bool> firstLoading =
                new TaskCompletionSource<bool>();
            private int loadingCount;

            public bool ThrowOnLoading { get; set; }
            public bool ThrowOnFailure { get; set; }

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                if (eventName == "viewer_loading" && ThrowOnLoading)
                {
                    throw new InvalidOperationException("Loading event route failed.");
                }

                if (eventName == "viewer_failed" && ThrowOnFailure)
                {
                    throw new InvalidOperationException("Failure event route failed.");
                }

                if (eventName == "viewer_loading" &&
                    Interlocked.Increment(ref loadingCount) == 1)
                {
                    // Deliberately ignore cancellation to verify the generation guard.
                    return firstLoading.Task;
                }

                return Task.CompletedTask;
            }

            public void ReleaseFirstLoading() => firstLoading.TrySetResult(true);
        }
    }
}
