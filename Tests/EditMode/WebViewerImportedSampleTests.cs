using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Deucarian.TemplateViewer;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerImportedSampleTests
    {
        private string folder;
        private SceneSetup[] originalScenes;

        [UnityTest]
        public IEnumerator ImportedSampleInitializesThroughItsConfiguredPlatformEndpoint()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("Run this imported-scene integration check in an isolated batch-mode test project; interactive user scenes must remain untouched.");
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Any(scene =>
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty), Is.False,
                "Save open scenes before running the imported-scene integration test.");
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            string name = "WebViewerSampleTest-" + Guid.NewGuid().ToString("N");
            folder = "Assets/" + name;
            Assert.That(AssetDatabase.CreateFolder("Assets", name), Is.Not.Empty);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WebViewerBootstrap).Assembly);
            FileUtil.CopyFileOrDirectory(Path.Combine(package.resolvedPath, "Samples~/Web Viewer/Scenes"), folder + "/Scenes");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(folder + "/Scenes/WebViewer.unity");
            foreach (var transform in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 12; frame++) yield return null;
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<WebViewerBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Application, Is.Not.Null);
            Assert.That(bootstrap.PlatformAdapter.EventEndpoint, Is.EqualTo("direct"));
            using var cancellation = new CancellationTokenSource(10000);
            var initialize = bootstrap.Application.InitializeAsync(new ViewerInitializeRequest { Revision = 1 },
                bootstrap.PlatformAdapter.EventEndpoint, cancellation.Token);
            double deadline = EditorApplication.timeSinceStartup + 12;
            while (!initialize.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(initialize.IsCompleted, Is.True);
            var result = initialize.GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True, result.ErrorCode + ": " + result.Message);
            Assert.That(bootstrap.Application.Lifecycle, Is.EqualTo(ViewerLifecycleState.Ready));
            Assert.That(bootstrap.Application.IndexedElementCount, Is.EqualTo(3));
            Assert.That(bootstrap.ReferenceNavigation, Is.Not.Null);
            Assert.That(bootstrap.CurrentTheme, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(camera => camera.enabled), Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (string.IsNullOrEmpty(folder)) yield break;
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
            folder = null;
            if (originalScenes != null && originalScenes.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
        }
    }
}
