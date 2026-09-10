using System;
using System.Collections.Generic;
using System.Reflection;
using Deucarian.API.Models;
using Deucarian.SimultriaViewerIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration.Tests
{
    public sealed class SimultriaWebViewerStartupStatusTests
    {
        private GameObject root;
        private SimultriaViewerBuildConnectionGate gate;
        private WebViewerBootstrap viewer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Explicit connection startup bridge");
            root.SetActive(false);
            gate = root.AddComponent<SimultriaViewerBuildConnectionGate>();
            viewer = root.AddComponent<WebViewerBootstrap>();
            typeof(SimultriaViewerBuildConnectionGate).GetField(
                "startupBehaviours", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(gate, new Behaviour[] { viewer });
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void SubscriptionReplaysThenStopsAfterDisposal()
        {
            var observed = new List<WebViewerStartupStatusProjection>();
            var subscription = new SimultriaWebViewerStartupSubscription(gate, viewer, observed.Add);
            Assert.That(observed.Count, Is.EqualTo(1), "The current gate snapshot must be replayed.");
            Assert.That(ObserverCount(), Is.EqualTo(1));
            Publish(new SimultriaViewerBuildStartupSnapshot(
                SimultriaViewerBuildStartupPhase.Resolving));
            Assert.That(observed[1].Phase, Is.EqualTo("resolving_environment"));

            subscription.Dispose();
            subscription.Dispose();
            Assert.That(ObserverCount(), Is.Zero, "Dispose must detach the actual gate event.");
            Publish(new SimultriaViewerBuildStartupSnapshot(
                SimultriaViewerBuildStartupPhase.Failed,
                SimultriaViewerBuildStartupFailureCode.EnvironmentResolutionFailed));
            Assert.That(observed.Count, Is.EqualTo(2));
        }

        [Test]
        public void GateDisposalEndsTheProjectionWithoutAFalseReadinessSignal()
        {
            var observed = new List<WebViewerStartupStatusProjection>();
            using (var subscription = new SimultriaWebViewerStartupSubscription(gate, viewer, observed.Add))
            {
                typeof(SimultriaViewerBuildConnectionGate).GetMethod(
                    "DisposeStartup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gate, null);
                Assert.That(ObserverCount(), Is.Zero);
                Publish(new SimultriaViewerBuildStartupSnapshot(
                    SimultriaViewerBuildStartupPhase.Resolving));
            }
            Assert.That(observed.Count, Is.EqualTo(1));
        }

        [Test]
        public void GateMustExplicitlyOwnTheSelectedViewer()
        {
            typeof(SimultriaViewerBuildConnectionGate).GetField(
                "startupBehaviours", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(gate, Array.Empty<Behaviour>());
            Assert.Throws<ArgumentException>(() =>
                new SimultriaWebViewerStartupSubscription(gate, viewer, _ => { }));
        }

        [Test]
        public void CachedGateFailureIsReplayedBeforeTheViewerStarts()
        {
            Publish(new SimultriaViewerBuildStartupSnapshot(
                SimultriaViewerBuildStartupPhase.Failed,
                SimultriaViewerBuildStartupFailureCode.EnvironmentResolutionFailed));
            var observed = new List<WebViewerStartupStatusProjection>();
            using (var subscription = new SimultriaWebViewerStartupSubscription(gate, viewer, observed.Add))
            {
                Assert.That(observed.Count, Is.EqualTo(1));
                Assert.That(observed[0].Failed, Is.True);
                Assert.That(observed[0].Value, Is.EqualTo("viewer_environment_resolution_failed"));
                Assert.That(viewer.Application, Is.Null);
                Assert.That(ObserverCount(), Is.EqualTo(1));
            }
            Assert.That(ObserverCount(), Is.Zero);
        }

        [Test]
        public void FailedInitialReplayDoesNotLeaveAnEventSubscription()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new SimultriaWebViewerStartupSubscription(gate, viewer,
                    _ => throw new InvalidOperationException("synthetic observer failure")));
            Assert.That(ObserverCount(), Is.Zero);
        }

        [Test]
        public void BridgeComponentReattachesOnceAndDetachesOnDisableAndDestroy()
        {
            var bridge = root.AddComponent<SimultriaWebViewerStartupBridge>();
            typeof(SimultriaWebViewerStartupBridge).GetField(
                "connectionGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, gate);
            typeof(SimultriaWebViewerStartupBridge).GetField(
                "viewerBootstrap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, viewer);

            InvokeBridge(bridge, "OnEnable");
            Assert.That(ObserverCount(), Is.EqualTo(1));
            InvokeBridge(bridge, "OnEnable");
            Assert.That(ObserverCount(), Is.EqualTo(1));
            InvokeBridge(bridge, "OnDisable");
            Assert.That(ObserverCount(), Is.Zero);
            InvokeBridge(bridge, "OnEnable");
            Assert.That(ObserverCount(), Is.EqualTo(1));
            InvokeBridge(bridge, "OnDestroy");
            Assert.That(ObserverCount(), Is.Zero);
        }

        [Test]
        public void BridgeComponentRejectsMissingExplicitViewerWithoutSubscribing()
        {
            var bridge = root.AddComponent<SimultriaWebViewerStartupBridge>();
            typeof(SimultriaWebViewerStartupBridge).GetField(
                "connectionGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, gate);
            Assert.DoesNotThrow(() => InvokeBridge(bridge, "OnEnable"));
            Assert.That(ObserverCount(), Is.Zero);
            Assert.That(bridge.BindingFailure,
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.ViewerMissing));
            Assert.That(SimultriaWebViewerStartupBridge.InvalidBindingShellCode,
                Is.EqualTo("viewer_composition_failed"));
        }

        [Test]
        public void BindingValidationReturnsOnlyFixedReasons()
        {
            Assert.That(SimultriaWebViewerStartupSubscription.ValidateBinding(gate, null),
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.ViewerMissing));
            Assert.That(SimultriaWebViewerStartupSubscription.ValidateBinding(null, viewer),
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.ConnectionGateMissing));
            Assert.That(SimultriaWebViewerStartupSubscription.ValidateBinding(gate, viewer),
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.None));
            typeof(SimultriaViewerBuildConnectionGate).GetField(
                "startupBehaviours", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(gate, Array.Empty<Behaviour>());
            Assert.That(SimultriaWebViewerStartupSubscription.ValidateBinding(gate, viewer),
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.ViewerNotOwned));
            Assert.That(Enum.GetNames(typeof(SimultriaWebViewerStartupBindingFailure)),
                Is.EqualTo(new[] { "None", "ViewerMissing", "ConnectionGateMissing",
                    "InvalidScene", "SceneMismatch", "ViewerNotOwned" }));
        }

        [Test]
        public void PersistentPrefabReferencesCannotBindAsALiveScene()
        {
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                "Assets/__DeucarianWebStartupBinding.prefab");
            try
            {
                var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
                Assert.That(SimultriaWebViewerStartupSubscription.ValidateBinding(
                    prefab.GetComponent<SimultriaViewerBuildConnectionGate>(),
                    prefab.GetComponent<WebViewerBootstrap>()),
                    Is.EqualTo(SimultriaWebViewerStartupBindingFailure.InvalidScene));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void RepairingABindingReplacesTheFixedLocalFailure()
        {
            var bridge = root.AddComponent<SimultriaWebViewerStartupBridge>();
            InvokeBridge(bridge, "OnEnable");
            Assert.That(bridge.BindingFailure,
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.ViewerMissing));
            typeof(SimultriaWebViewerStartupBridge).GetField(
                "connectionGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, gate);
            typeof(SimultriaWebViewerStartupBridge).GetField(
                "viewerBootstrap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, viewer);
            InvokeBridge(bridge, "OnEnable");
            Assert.That(bridge.BindingFailure, Is.EqualTo(SimultriaWebViewerStartupBindingFailure.None));
            Assert.That(ObserverCount(), Is.EqualTo(1));
            InvokeBridge(bridge, "OnDisable");
            Assert.That(ObserverCount(), Is.Zero);
        }

        [TestCase("simultria.production", "production")]
        [TestCase("simultria.development", "development")]
        [TestCase("simultria.testing", "testing")]
        [TestCase("simultria.acceptance", "acceptance")]
        public void FallbackNoticeContainsOnlyTheFixedShellIdentifier(string environment, string shellIdentifier)
        {
            var projection = WebViewerStartupStatusProjection.From(
                new SimultriaViewerBuildStartupSnapshot(
                    SimultriaViewerBuildStartupPhase.Fallback,
                    environmentId: new ApiEnvironmentId(environment)));
            Assert.That(projection.Failed, Is.False);
            Assert.That(projection.Phase, Is.EqualTo("build_profile_fallback"));
            Assert.That(projection.Value, Is.EqualTo(shellIdentifier));
        }

        [TestCase("simultria.local")]
        [TestCase("custom.private")]
        public void LocalOrUnknownFallbackNeverCrossesThePageBoundary(string environment)
        {
            var projection = WebViewerStartupStatusProjection.From(
                new SimultriaViewerBuildStartupSnapshot(
                    SimultriaViewerBuildStartupPhase.Fallback,
                    environmentId: new ApiEnvironmentId(environment)));
            Assert.That(projection.Failed, Is.True);
            Assert.That(projection.Phase, Is.Null);
            Assert.That(projection.Value, Is.EqualTo("viewer_environment_resolution_failed"));
        }

        [Test]
        public void ExactRoutingDoesNotClaimReadinessOrShowAFallback()
        {
            var projection = WebViewerStartupStatusProjection.From(
                new SimultriaViewerBuildStartupSnapshot(
                    SimultriaViewerBuildStartupPhase.Routed,
                    environmentId: new ApiEnvironmentId("simultria.production")));
            Assert.That(projection.Failed, Is.False);
            Assert.That(projection.Phase, Is.Null);
            Assert.That(projection.Value, Is.Null);
        }

        [TestCase(SimultriaViewerBuildStartupFailureCode.EnvironmentResolutionFailed,
            "viewer_environment_resolution_failed")]
        [TestCase(SimultriaViewerBuildStartupFailureCode.ProviderCreationFailed, "viewer_connection_failed")]
        [TestCase(SimultriaViewerBuildStartupFailureCode.ProviderRegistrationFailed, "viewer_connection_failed")]
        [TestCase(SimultriaViewerBuildStartupFailureCode.EnvironmentActivationFailed, "viewer_connection_failed")]
        [TestCase(SimultriaViewerBuildStartupFailureCode.StartupCancelled, "viewer_connection_failed")]
        public void FailuresUseOnlyStaticShellCodes(
            SimultriaViewerBuildStartupFailureCode failure, string expected)
        {
            var projection = WebViewerStartupStatusProjection.From(
                new SimultriaViewerBuildStartupSnapshot(SimultriaViewerBuildStartupPhase.Failed, failure));
            Assert.That(projection.Failed, Is.True);
            Assert.That(projection.Value, Is.EqualTo(expected));
            Assert.That(projection.Phase, Is.Null);
        }

        private void Publish(SimultriaViewerBuildStartupSnapshot snapshot) =>
            typeof(SimultriaViewerBuildConnectionGate).GetMethod(
                "Publish", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gate, new object[] { snapshot });

        private int ObserverCount() =>
            ((Delegate)typeof(SimultriaViewerBuildConnectionGate).GetField(
                "StartupStatusChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gate))
                ?.GetInvocationList().Length ?? 0;

        private static void InvokeBridge(SimultriaWebViewerStartupBridge bridge, string method) =>
            typeof(SimultriaWebViewerStartupBridge).GetMethod(
                method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bridge, null);
    }
}
