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
    public sealed class WebViewerModelOriginPreparationTests
    {
        [Test]
        public async Task PlacementAndCenteringPrecedeVisibilityAndPreserveState()
        {
            GameObject model = new GameObject("Model Presentation Root");
            GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject navigationObject = new GameObject("Navigation");
            GameObject cameraObject = new GameObject("Camera");
            WebViewerApplication application = null;
            try
            {
                element.transform.SetParent(model.transform, false);
                element.transform.localPosition = new Vector3(7f, 2f, -3f);
                element.transform.localRotation = Quaternion.Euler(5f, 11f, 17f);
                element.transform.localScale = new Vector3(2f, 3f, 4f);
                Vector3 authoredPosition = element.transform.localPosition;
                Quaternion authoredRotation = element.transform.localRotation;
                Vector3 authoredScale = element.transform.localScale;
                element.SetActive(false);

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    new Vector3(13f, 10f, -20f),
                    Quaternion.Euler(22f, -34f, 0f));
                ViewerNavigationInstaller navigation =
                    navigationObject.AddComponent<ViewerNavigationInstaller>();
                navigation.Initialize(camera, null);
                var visibility = new RecordingVisibilityFactory(element);
                application = new WebViewerApplication(
                    new DirectWebViewerModelDescriptorResolver(),
                    new EmbeddedOnlyModelLoader(),
                    navigation,
                    new SilentEventPublisher(),
                    model,
                    null,
                    visibility);

                var placement = new WebViewerModelPlacement(
                    new Vector3(12f, -4f, 8f),
                    new Vector3(0f, 90f, 0f),
                    new Vector3(1.5f, 2f, 0.75f));
                CommandOperationResult first = await application.InitializeAsync(
                    new WebViewerInitializeRequest
                    {
                        Revision = 1,
                        ModelPlacement = placement,
                        CenterModelOnWorldOrigin = true
                    },
                    "test-host",
                    CancellationToken.None);

                Assert.That(first.Succeeded, Is.True, first.Message);
                Assert.That(visibility.CreationCount, Is.EqualTo(1));
                Assert.That(visibility.WasElementActiveAtCreation, Is.False);
                Assert.That(
                    visibility.BoundsCenterAtCreation.sqrMagnitude,
                    Is.LessThan(0.0001f));
                Assert.That(element.activeSelf, Is.False);
                Assert.That(element.transform.localPosition, Is.EqualTo(authoredPosition));
                Assert.That(element.transform.localRotation, Is.EqualTo(authoredRotation));
                Assert.That(element.transform.localScale, Is.EqualTo(authoredScale));
                Assert.That(
                    Quaternion.Angle(
                        model.transform.localRotation,
                        Quaternion.Euler(placement.RotationEuler)),
                    Is.LessThan(0.001f));
                Assert.That(model.transform.localScale, Is.EqualTo(placement.Scale));
                Assert.That(
                    navigation.Controller.ReferenceBounds.center.sqrMagnitude,
                    Is.LessThan(0.0001f));
                Vector3 firstPosition = model.transform.position;

                CommandOperationResult second = await application.InitializeAsync(
                    new WebViewerInitializeRequest
                    {
                        Revision = 2,
                        ModelPlacement = placement,
                        CenterModelOnWorldOrigin = true
                    },
                    "test-host",
                    CancellationToken.None);

                Assert.That(second.Succeeded, Is.True, second.Message);
                Assert.That(visibility.CreationCount, Is.EqualTo(2));
                Assert.That(model.transform.position, Is.EqualTo(firstPosition));
                Assert.That(element.activeSelf, Is.False);
                Assert.That(element.transform.localPosition, Is.EqualTo(authoredPosition));
                Assert.That(element.transform.localRotation, Is.EqualTo(authoredRotation));
                Assert.That(element.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                application?.Dispose();
                UnityEngine.Object.DestroyImmediate(model);
                UnityEngine.Object.DestroyImmediate(navigationObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class RecordingVisibilityFactory :
            IWebViewerVisibilityFeatureFactory
        {
            private readonly GameObject element;

            public RecordingVisibilityFactory(GameObject modelElement)
            {
                element = modelElement;
            }

            public int CreationCount { get; private set; }
            public bool WasElementActiveAtCreation { get; private set; }
            public Vector3 BoundsCenterAtCreation { get; private set; }

            public bool TryCreate(
                WebViewerModelContext context,
                out IWebViewerVisibilityFeature feature,
                out string error)
            {
                CreationCount++;
                WasElementActiveAtCreation = element.activeSelf;
                var bounds = new ViewerNavigationMeshBoundsStrategy();
                bounds.TryGetBounds(context.ReferenceRoot, out Bounds measured);
                BoundsCenterAtCreation = measured.center;
                feature = new PassiveVisibilityFeature();
                error = string.Empty;
                return true;
            }
        }

        private sealed class PassiveVisibilityFeature :
            IWebViewerVisibilityFeature
        {
            public int IndexedElementCount => 1;
            public int SelectedElementCount => 0;
            public void Dispose()
            {
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

        private sealed class SilentEventPublisher : IWebViewerEventPublisher
        {
            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}
