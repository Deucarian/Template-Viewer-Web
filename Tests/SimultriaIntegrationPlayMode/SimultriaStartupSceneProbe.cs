using System;
using System.Reflection;
using Deucarian.SimultriaViewerIntegration;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration.Tests
{
    // Test-only serialized MonoBehaviour. Runs after the bridge's real OnEnable,
    // while the cold scene is still loading. No payloads or networking are used.
    [DefaultExecutionOrder(-10999)]
    public sealed class SimultriaStartupSceneProbe : MonoBehaviour
    {
        public SimultriaViewerBuildConnectionGate gate;
        public WebViewerBootstrap viewer;
        public SimultriaWebViewerStartupBridge bridge;

        public static SimultriaStartupSceneProbe LastEnabled { get; private set; }
        public bool ViewerPresent { get; private set; }
        public bool GatePresent { get; private set; }
        public bool SceneValid { get; private set; }
        public bool SceneLoadedDuringEnable { get; private set; }
        public bool SameScene { get; private set; }
        public bool ExplicitOwnership { get; private set; }
        public bool BridgeAttached { get; private set; }
        public int ObserverCountDuringEnable { get; private set; }

        private void OnEnable()
        {
            LastEnabled = this;
            ViewerPresent = viewer != null;
            GatePresent = gate != null;
            SceneValid = ViewerPresent && viewer.gameObject.scene.IsValid();
            SceneLoadedDuringEnable = ViewerPresent && viewer.gameObject.scene.isLoaded;
            SameScene = ViewerPresent && GatePresent &&
                gate.gameObject.scene == viewer.gameObject.scene &&
                bridge.gameObject.scene == viewer.gameObject.scene;
            ExplicitOwnership = GatePresent && gate.ContainsStartupBehaviour(viewer);
            // Narrow test-only inspection of the two private ownership fields.
            BridgeAttached = typeof(SimultriaWebViewerStartupBridge).GetField(
                "subscription", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(bridge) != null;
            ObserverCountDuringEnable = ObserverCount(gate);
        }

        public static int ObserverCount(SimultriaViewerBuildConnectionGate target) =>
            ((Delegate)typeof(SimultriaViewerBuildConnectionGate).GetField(
                "StartupStatusChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target))?.GetInvocationList().Length ?? 0;

        public static void ResetObservation() => LastEnabled = null;
    }
}
