#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration.Tests
{
    public sealed class SimultriaWebViewerColdSceneTests
    {
        private const string ScenePath = "Packages/com.deucarian.template.viewer.web/" +
            "Tests/SimultriaIntegrationPlayMode/Scenes/ColdStartup.unity";
        private const string OtherSceneName = "DeucarianWebStartupOtherScene";

        [UnityTest]
        public IEnumerator SerializedColdSceneAttachesBeforeLoadedAndDetachesOnUnload()
        {
            SimultriaStartupSceneProbe.ResetObservation();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));

            var probe = SimultriaStartupSceneProbe.LastEnabled;
            Assert.That(probe, Is.Not.Null, "The serialized probe must receive real OnEnable.");
            TestContext.WriteLine("cold_scene_guard " +
                "viewer_present=" + probe.ViewerPresent +
                " gate_present=" + probe.GatePresent +
                " scene_valid=" + probe.SceneValid +
                " scene_loaded=" + probe.SceneLoadedDuringEnable +
                " same_scene=" + probe.SameScene +
                " explicit_ownership=" + probe.ExplicitOwnership +
                " bridge_attached=" + probe.BridgeAttached +
                " observer_count=" + probe.ObserverCountDuringEnable);
            Assert.That(probe.ViewerPresent && probe.GatePresent && probe.SceneValid &&
                probe.SameScene && probe.ExplicitOwnership, Is.True);
            Assert.That(probe.SceneLoadedDuringEnable, Is.False,
                "This regression must exercise cold scene loading, not a warm object activation.");
            Assert.That(probe.gate.gameObject.activeSelf, Is.False,
                "The fixture must not start a connection or network request.");
            Assert.That(probe.viewer.enabled, Is.False);
            Assert.That(probe.viewer.Application, Is.Null);
            Assert.That(probe.BridgeAttached, Is.True,
                "A valid explicit bridge must attach during initial scene activation.");
            Assert.That(probe.ObserverCountDuringEnable, Is.EqualTo(1));
            Assert.That(probe.bridge.BindingFailure, Is.EqualTo(SimultriaWebViewerStartupBindingFailure.None));

            var gate = probe.gate;
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(ScenePath));
            Assert.That(SimultriaStartupSceneProbe.ObserverCount(gate), Is.Zero,
                "Real scene unloading must detach the bridge without manual callbacks.");
        }

        [UnityTest]
        public IEnumerator MovingOwnedViewerToAnotherSceneRejectsReenabledBridge()
        {
            SimultriaStartupSceneProbe.ResetObservation();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            var probe = SimultriaStartupSceneProbe.LastEnabled;
            Assert.That(probe, Is.Not.Null);
            Assert.That(probe.BridgeAttached, Is.True);
            probe.bridge.enabled = false;
            Assert.That(SimultriaStartupSceneProbe.ObserverCount(probe.gate), Is.Zero);

            Scene originalScene = probe.viewer.gameObject.scene;
            Scene otherScene = SceneManager.CreateScene(OtherSceneName);
            SceneManager.MoveGameObjectToScene(probe.viewer.gameObject, otherScene);
            Assert.That(probe.gate.ContainsStartupBehaviour(probe.viewer), Is.True);
            probe.bridge.enabled = true;
            Assert.That(probe.bridge.BindingFailure,
                Is.EqualTo(SimultriaWebViewerStartupBindingFailure.SceneMismatch));
            Assert.That(SimultriaStartupSceneProbe.ObserverCount(probe.gate), Is.Zero);

            probe.bridge.enabled = false;
            SceneManager.MoveGameObjectToScene(probe.viewer.gameObject, originalScene);
            probe.bridge.enabled = true;
            Assert.That(probe.bridge.BindingFailure, Is.EqualTo(SimultriaWebViewerStartupBindingFailure.None));
            Assert.That(SimultriaStartupSceneProbe.ObserverCount(probe.gate), Is.EqualTo(1));
            Assert.That(probe.gate.gameObject.activeSelf, Is.False);
            Assert.That(probe.viewer.enabled, Is.False);
            Assert.That(probe.viewer.Application, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            Scene otherScene = SceneManager.GetSceneByName(OtherSceneName);
            if (otherScene.IsValid() && otherScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(otherScene);
            SimultriaStartupSceneProbe.ResetObservation();
        }
    }
}
#endif
