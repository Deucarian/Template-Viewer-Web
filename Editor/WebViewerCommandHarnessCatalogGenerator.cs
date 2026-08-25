using System;
using System.Collections.Generic;
using System.IO;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewerWeb.Commands;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewerWeb.Editor
{
    public static class WebViewerCommandHarnessCatalogGenerator
    {
        public const string DefaultCatalogPath =
            "Library/Deucarian/WebViewerHarness/commands.generated.json";

        public static WebViewerCommandHarnessCatalog CreateCatalog(
            WebViewerBootstrap bootstrap)
        {
            if (bootstrap == null)
            {
                throw new ArgumentNullException(nameof(bootstrap));
            }

            WebViewerFeatureBehaviour[] features =
                bootstrap.GetComponents<WebViewerFeatureBehaviour>();
            bool hasProductVisibility = false;
            for (int index = 0; index < features.Length; index++)
            {
                if (features[index] != null &&
                    features[index].VisibilityFeatureFactory != null)
                {
                    hasProductVisibility = true;
                    break;
                }
            }

            var handlers = new List<ICommandHandler<WebViewerApplication>>(
                WebViewerCommandHandlers.Create(
                    includeGenericVisibilityCommands: !hasProductVisibility,
                    initializationHandler: WebViewerFeatureComposition
                        .ResolveInitializationCommandHandler(features)));
            var scenarios = new List<WebViewerCommandHarnessScenario>(
                WebViewerCommandHarnessCatalogBuilder.CreateGenericScenarios(
                    includeGenericVisibilityCommands: !hasProductVisibility));
            WebViewerCommandHarnessScenario disposeScenario =
                features.Length > 0
                    ? scenarios.Find(value => value.Id == "dispose")
                    : null;
            if (disposeScenario != null)
            {
                scenarios.Remove(disposeScenario);
            }

            for (int index = 0; index < features.Length; index++)
            {
                WebViewerFeatureBehaviour feature = features[index];
                if (feature == null)
                {
                    continue;
                }

                IReadOnlyList<ICommandHandler<WebViewerApplication>>
                    featureHandlers = feature.CreateCommandHandlers();
                if (featureHandlers != null)
                {
                    handlers.AddRange(featureHandlers);
                }

                IReadOnlyList<WebViewerCommandHarnessScenario>
                    featureScenarios =
                        feature.CreateCommandHarnessScenarios();
                if (featureScenarios != null)
                {
                    scenarios.AddRange(featureScenarios);
                }
            }

            if (disposeScenario != null)
            {
                scenarios.Add(disposeScenario);
            }

            return WebViewerCommandHarnessCatalogBuilder.Create(
                handlers,
                scenarios);
        }

        public static WebViewerCommandHarnessCatalog GenerateForScene(
            string scenePath,
            string catalogPath = DefaultCatalogPath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException(
                    "A viewer scene path is required.",
                    nameof(scenePath));
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForGeneration = !scene.IsValid() || !scene.isLoaded;
            if (openedForGeneration)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    throw new FileNotFoundException(
                        "The viewer scene could not be found.",
                        scenePath);
                }

                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WebViewerBootstrap bootstrap = FindBootstrap(scene);
                if (bootstrap == null)
                {
                    throw new InvalidOperationException(
                        "The viewer scene has no WebViewerBootstrap.");
                }

                WebViewerCommandHarnessCatalog catalog =
                    CreateCatalog(bootstrap);
                Write(catalog, catalogPath);
                return catalog;
            }
            finally
            {
                if (openedForGeneration && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public static void Write(
            WebViewerCommandHarnessCatalog catalog,
            string catalogPath = DefaultCatalogPath)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                throw new ArgumentException(
                    "A catalog output path is required.",
                    nameof(catalogPath));
            }

            string absolutePath = Path.IsPathRooted(catalogPath)
                ? Path.GetFullPath(catalogPath)
                : Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    catalogPath));
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "The catalog output directory is invalid.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(
                absolutePath,
                JsonConvert.SerializeObject(catalog, Formatting.Indented) +
                Environment.NewLine);
        }

        private static WebViewerBootstrap FindBootstrap(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                WebViewerBootstrap bootstrap =
                    roots[index].GetComponentInChildren<WebViewerBootstrap>(
                        true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
        }
    }
}
