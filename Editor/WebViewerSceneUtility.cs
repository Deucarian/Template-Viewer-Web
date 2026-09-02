using System;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using Deucarian.TemplateViewer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewerWeb.Editor
{
    internal static class WebViewerSceneUtility
    {
        internal static void ConfigureProductScene(
            WebViewerProductBuildConfiguration configuration)
        {
            Scene scene = OpenScene(configuration.ScenePath, out bool opened);
            try
            {
                IReadOnlyList<ViewerBootstrap> bootstraps =
                    FindComponents<ViewerBootstrap>(scene);
                if (bootstraps.Count != 1 ||
                    !(bootstraps[0] is WebViewerBootstrap bootstrap))
                {
                    throw new InvalidOperationException(
                        "The real product scene must contain exactly one Web " +
                        "Viewer Bootstrap before profiles can be synchronized.");
                }

                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("iframeMode").boolValue = false;
                serialized.FindProperty("parentOrigin").stringValue =
                    string.Empty;
                serialized.FindProperty("transportId").stringValue =
                    configuration.TransportId;
                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                CloseScene(scene, opened);
            }
        }

        internal static DeucarianBuildValidationResult ValidateProductScene(
            WebViewerProductBuildConfiguration configuration,
            bool production)
        {
            var result = new DeucarianBuildValidationResult();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    configuration.ScenePath) == null)
            {
                result.Add(
                    "The real Web Viewer product scene is missing at '" +
                    configuration.ScenePath + "'.");
                return result;
            }

            Scene scene = default;
            bool opened = false;
            try
            {
                scene = OpenScene(configuration.ScenePath, out opened);
                IReadOnlyList<ViewerBootstrap> bootstraps =
                    FindComponents<ViewerBootstrap>(scene);
                result.AddRange(ValidateViewerComposition(
                    bootstraps,
                    production));
                WebViewerBootstrap web = bootstraps.Count == 1
                    ? bootstraps[0] as WebViewerBootstrap
                    : null;
                if (web != null &&
                    (web.IframeMode ||
                     !string.IsNullOrWhiteSpace(web.ParentOrigin) ||
                     !string.Equals(
                         web.TransportId,
                         configuration.TransportId,
                         StringComparison.Ordinal)))
                {
                    result.Add(
                        "The Web Viewer Bootstrap must use transport '" +
                        configuration.TransportId +
                        "' with deployment-configured iframe origin.");
                }

                IReadOnlyList<ViewerFeatureBehaviour> features =
                    FindComponents<ViewerFeatureBehaviour>(scene);
                if (features.Count != 1)
                {
                    result.Add(
                        "The real product scene contains " + features.Count +
                        " ViewerFeatureBehaviour domain features; exactly " +
                        "one is required.");
                }
                else if (configuration.RequiredDomainFeatureType == null ||
                         features[0].GetType() !=
                         configuration.RequiredDomainFeatureType)
                {
                    result.Add(
                        "The real product scene must contain exactly the " +
                        "declared domain feature '" +
                        (configuration.RequiredDomainFeatureType?.FullName ??
                         "<missing>") + "'.");
                }

                if (web != null && features.Count == 1)
                {
                    try
                    {
                        IReadOnlyList<ViewerFeatureBehaviour> resolved =
                            web.ResolvedFeatureBehaviours;
                        if (resolved.Count != 1 ||
                            !ReferenceEquals(resolved[0], features[0]))
                        {
                            result.Add(
                                "The Web Viewer Bootstrap must resolve exactly " +
                                "the declared domain feature. Place it beside " +
                                "the bootstrap or assign it explicitly.");
                        }
                    }
                    catch (Exception exception)
                    {
                        result.Add(
                            "The Web Viewer Bootstrap domain-feature " +
                            "composition is invalid (" +
                            exception.GetType().Name + ").");
                    }
                }
            }
            catch (Exception exception)
            {
                result.Add(
                    "The real Web Viewer product scene could not be " +
                    "validated (" + exception.GetType().Name + ").");
            }
            finally
            {
                CloseScene(scene, opened);
            }

            return result;
        }

        internal static IReadOnlyList<string> ValidateViewerComposition(
            IReadOnlyList<ViewerBootstrap> bootstraps,
            bool production)
        {
            int count = bootstraps?.Count ?? 0;
            if (count == 0)
            {
                return new[] { "The viewer scene has no ViewerBootstrap." };
            }

            if (count != 1)
            {
                return new[]
                {
                    "The viewer scene contains " + count +
                    " ViewerBootstrap components; exactly one platform " +
                    "adapter is required."
                };
            }

            if (!(bootstraps[0] is WebViewerBootstrap webBootstrap))
            {
                return new[]
                {
                    "A Web Viewer build requires WebViewerBootstrap as its " +
                    "only ViewerBootstrap."
                };
            }

            return webBootstrap.TryValidateConfiguration(
                production,
                out string issue)
                ? Array.Empty<string>()
                : new[] { issue };
        }

        internal static IReadOnlyList<T> FindComponents<T>(Scene scene)
            where T : Component
        {
            var values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                values.AddRange(
                    roots[i].GetComponentsInChildren<T>(true));
            }

            return values;
        }

        private static Scene OpenScene(string path, out bool opened)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : scene;
        }

        private static void CloseScene(Scene scene, bool opened)
        {
            if (opened && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
