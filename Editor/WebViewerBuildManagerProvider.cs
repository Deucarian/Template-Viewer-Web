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
    public sealed class WebViewerBuildManagerProvider :
        IDeucarianBuildManagerProvider
    {
        public const string DevelopmentProfilePath =
            "Assets/Deucarian/WebViewer/BuildProfiles/WebViewer-Development.asset";
        public const string ProductionProfilePath =
            "Assets/Deucarian/WebViewer/BuildProfiles/WebViewer-Production.asset";
        public const string DevelopmentScenePath =
            "Assets/Deucarian/WebViewer/Scenes/WebViewer-Development.unity";
        public const string ProductionScenePath =
            "Assets/Deucarian/WebViewer/Scenes/WebViewer-Production.unity";

        public string Id => "template-viewer-web";
        public string DisplayName => "Web Viewer Template";
        public int Order => 300;
        public bool CanSynchronize => true;

        public IReadOnlyList<DeucarianBuildManagerTarget> GetTargets()
        {
            return new[]
            {
                CreateTarget(
                    "development",
                    "Web Viewer - Development",
                    DevelopmentProfilePath,
                    DevelopmentScenePath,
                    DeucarianBuildEnvironment.Development,
                    "Builds/WebViewer-Development"),
                CreateTarget(
                    "production",
                    "Web Viewer - Production",
                    ProductionProfilePath,
                    ProductionScenePath,
                    DeucarianBuildEnvironment.Production,
                    "Builds/WebViewer-Production")
            };
        }

        public void Synchronize()
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
                DevelopmentProfilePath,
                DevelopmentScenePath,
                DeucarianBuildEnvironment.Development);
            SynchronizeProfile(
                ProductionProfilePath,
                ProductionScenePath,
                DeucarianBuildEnvironment.Production);
        }

        private static DeucarianBuildManagerTarget CreateTarget(
            string id,
            string displayName,
            string profilePath,
            string scenePath,
            DeucarianBuildEnvironment environment,
            string outputPath)
        {
            return new DeucarianBuildManagerTarget(
                id,
                displayName,
                "Builds the generic browser-hosted viewer through a project-owned WebGL Build Profile.",
                profilePath,
                environment,
                outputPath,
                invocation => DeucarianBuildRunner.Build(
                    new DeucarianBuildRequest(
                        invocation.BuildProfile,
                        environment,
                        invocation.OutputPath,
                        invocation.AdditionalBuildOptions)),
                () => ValidateScene(profilePath, scenePath, environment));
        }

        private static void SynchronizeProfile(
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
            DeucarianBuildRunner.ApplyPolicy(profile, environment);
            DeucarianWebGLTemplate.ApplyTo(profile);
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
                GameObject root = new GameObject("Web Viewer Bootstrap");
                SceneManager.MoveGameObjectToScene(root, scene);
                WebViewerBootstrap bootstrap = root.AddComponent<WebViewerBootstrap>();
                SerializedObject serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("iframeMode").boolValue = iframeMode;
                serialized.FindProperty("parentOrigin").stringValue = parentOrigin;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
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
                result.Add("The project-owned viewer scene is missing: " + scenePath + ".");
                return result;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                WebViewerBootstrap bootstrap = FindBootstrap(scene);
                if (bootstrap == null)
                {
                    result.Add("The viewer scene has no WebViewerBootstrap.");
                }
                else if (!bootstrap.TryValidateConfiguration(
                             environment == DeucarianBuildEnvironment.Production,
                             out string issue))
                {
                    result.Add(issue);
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return result;
        }

        private static WebViewerBootstrap FindBootstrap(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                WebViewerBootstrap bootstrap =
                    roots[i].GetComponentInChildren<WebViewerBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
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
