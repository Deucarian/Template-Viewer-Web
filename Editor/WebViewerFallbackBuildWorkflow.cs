using System;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using Deucarian.WebGLTemplate.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewerWeb.Editor
{
    internal static class WebViewerFallbackBuildWorkflow
    {
        internal const string DevelopmentScenePath =
            "Assets/Deucarian/WebViewer/Scenes/" +
            "WebViewer-Development.unity";
        internal const string ProductionScenePath =
            "Assets/Deucarian/WebViewer/Scenes/" +
            "WebViewer-Production.unity";

        internal static IReadOnlyList<DeucarianBuildManagerTarget>
            CreateTargets() => new[]
            {
                CreateTarget(
                    "development",
                    "Web Viewer - Development",
                    WebViewerProductBuildWorkflow.DevelopmentProfilePath,
                    DevelopmentScenePath,
                    DeucarianBuildEnvironment.Development,
                    "Builds/WebViewer-Development"),
                CreateTarget(
                    "production",
                    "Web Viewer - Production",
                    WebViewerProductBuildWorkflow.ProductionProfilePath,
                    ProductionScenePath,
                    DeucarianBuildEnvironment.Production,
                    "Builds/WebViewer-Production")
            };

        internal static void Synchronize()
        {
            DeucarianWebGLTemplate.Synchronize();
            EnsureScene(
                DevelopmentScenePath,
                iframeMode: true,
                parentOrigin: "http://localhost:8080");
            EnsureScene(
                ProductionScenePath,
                iframeMode: false,
                parentOrigin: string.Empty);
            SynchronizeProfile(
                WebViewerProductBuildWorkflow.DevelopmentProfilePath,
                DevelopmentScenePath,
                DeucarianBuildEnvironment.Development);
            SynchronizeProfile(
                WebViewerProductBuildWorkflow.ProductionProfilePath,
                ProductionScenePath,
                DeucarianBuildEnvironment.Production);
            WebViewerCommandHarnessCatalogGenerator.GenerateForScene(
                DevelopmentScenePath);
        }

        private static DeucarianBuildManagerTarget CreateTarget(
            string id,
            string displayName,
            string profilePath,
            string scenePath,
            DeucarianBuildEnvironment environment,
            string outputPath) =>
            new DeucarianBuildManagerTarget(
                id,
                displayName,
                "Builds the generic browser-hosted viewer through a " +
                "project-owned WebGL Build Profile.",
                profilePath,
                environment,
                outputPath,
                invocation => Execute(
                    environment,
                    new DeucarianBuildRequest(
                        invocation.BuildProfile,
                        environment,
                        invocation.OutputPath,
                        invocation.AdditionalBuildOptions),
                    DeucarianBuildRunner.BuildWithOutputPreparation,
                    DeucarianBuildRunner.Build),
                () => ValidateScene(profilePath, scenePath, environment),
                requireCompleteResult: true);

        internal static DeucarianBuildResult Execute(
            DeucarianBuildEnvironment environment,
            DeucarianBuildRequest request,
            Func<DeucarianBuildRequest, DeucarianBuildResult> preparedBuild,
            Func<DeucarianBuildRequest, DeucarianBuildResult> build)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            if (WebViewerProductBuildWorkflow.RequiresOutputPreparation(
                    environment,
                    request))
            {
                if (preparedBuild == null)
                {
                    throw new ArgumentNullException(nameof(preparedBuild));
                }

                return preparedBuild(request);
            }

            return build(request);
        }

        internal static void SynchronizeProfile(
            string profilePath,
            string scenePath,
            DeucarianBuildEnvironment environment)
        {
            BuildProfile profile = DeucarianBuildProfileUtility.CreateProfile(
                BuildTarget.WebGL,
                profilePath);
            DeucarianBuildProfileUtility.ApplySceneOverride(
                profile,
                new EditorBuildSettingsScene(scenePath, true));
            profile = WebViewerProductBuildWorkflow.ReloadProfile(
                profilePath,
                "scene synchronization");
            DeucarianBuildRunner.ApplyPolicy(profile, environment);
            profile = WebViewerProductBuildWorkflow.ReloadProfile(
                profilePath,
                "build policy synchronization");
            DeucarianWebGLTemplate.ApplyTo(profile);
            WebViewerProductBuildWorkflow.ReloadProfile(
                profilePath,
                "WebGL template synchronization");
        }

        private static DeucarianBuildValidationResult ValidateScene(
            string profilePath,
            string scenePath,
            DeucarianBuildEnvironment environment)
        {
            var result = new DeucarianBuildValidationResult();
            BuildProfile profile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            result.AddRange(DeucarianWebGLTemplate.Validate(profile).Issues);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                result.Add(
                    "The project-owned viewer scene is missing: " +
                    scenePath + ".");
                return result;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                result.AddRange(
                    WebViewerSceneUtility.ValidateViewerComposition(
                        WebViewerSceneUtility.FindComponents<
                            Deucarian.TemplateViewer.ViewerBootstrap>(scene),
                        environment ==
                        DeucarianBuildEnvironment.Production));
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return result;
        }

        private static void EnsureScene(
            string scenePath,
            bool iframeMode,
            string parentOrigin)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                return;
            }

            EnsureAssetFolder(scenePath);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Web Viewer Bootstrap");
                SceneManager.MoveGameObjectToScene(root, scene);
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("iframeMode").boolValue = iframeMode;
                serialized.FindProperty("parentOrigin").stringValue =
                    parentOrigin;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = System.IO.Path.GetDirectoryName(assetPath)
                ?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || directory == "Assets")
            {
                return;
            }

            string current = "Assets";
            string[] parts = directory.Substring("Assets".Length)
                .Trim('/')
                .Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
