using System;
using System.Collections.Generic;
using System.IO;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer;
using Deucarian.TemplateViewer.Commands;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewerWeb.Editor
{
    public static class WebViewerCommandHarnessCatalogGenerator
    {
        public const string DefaultTransportId = "web-viewer";
        public const string DefaultCatalogPath =
            "Library/Deucarian/WebViewerHarness/commands.generated.json";

        public static ViewerCommandHarnessCatalog CreateCatalog(
            WebViewerBootstrap bootstrap)
        {
            if (bootstrap == null)
            {
                throw new ArgumentNullException(nameof(bootstrap));
            }

            IReadOnlyList<ViewerFeatureBehaviour> features =
                bootstrap.ResolvedFeatureBehaviours;
            bool hasProductVisibility = false;
            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] != null &&
                    features[index].VisibilityFeatureFactory != null)
                {
                    hasProductVisibility = true;
                    break;
                }
            }

            var handlers = new List<ICommandHandler<ViewerApplication>>(
                ViewerCommandHandlers.CreateDefault(
                    includeGenericVisibilityCommands: !hasProductVisibility,
                    initializationHandler: ViewerFeatureComposition
                        .ResolveInitializationCommandHandler(features)));
            var scenarios = new List<ViewerCommandHarnessScenario>(
                ViewerCommandHarnessCatalogBuilder.CreateGenericScenarios(
                    includeGenericVisibilityCommands: !hasProductVisibility));
            ViewerCommandHarnessScenario disposeScenario =
                features.Count > 0
                    ? scenarios.Find(value => value.Id == "dispose")
                    : null;
            if (disposeScenario != null)
            {
                scenarios.Remove(disposeScenario);
            }

            for (int index = 0; index < features.Count; index++)
            {
                ViewerFeatureBehaviour feature = features[index];
                if (feature == null)
                {
                    continue;
                }

                IReadOnlyList<ICommandHandler<ViewerApplication>>
                    featureHandlers = feature.CreateCommandHandlers();
                if (featureHandlers != null)
                {
                    handlers.AddRange(featureHandlers);
                }

                IReadOnlyList<ViewerCommandHarnessScenario>
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

            return ViewerCommandHarnessCatalogBuilder.Create(
                handlers,
                scenarios);
        }

        public static ViewerCommandHarnessCatalog GenerateForScene(
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

                ViewerCommandHarnessCatalog catalog =
                    CreateCatalog(bootstrap);
                Write(catalog, bootstrap.TransportId, catalogPath);
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
            ViewerCommandHarnessCatalog catalog,
            string catalogPath = DefaultCatalogPath) =>
            Write(catalog, DefaultTransportId, catalogPath);

        internal static void Write(
            ViewerCommandHarnessCatalog catalog,
            string transportId,
            string catalogPath)
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
                Serialize(catalog, transportId) +
                Environment.NewLine);
        }

        internal static string Serialize(
            ViewerCommandHarnessCatalog catalog,
            string transportId)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            string normalizedTransportId = NormalizeTransportId(transportId);
            return JsonConvert.SerializeObject(
                new
                {
                    schema_version = catalog.SchemaVersion,
                    transport_id = normalizedTransportId,
                    default_scenario_id = catalog.DefaultScenarioId,
                    scenarios = catalog.Scenarios
                },
                Formatting.Indented);
        }

        private static string NormalizeTransportId(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || normalized.Length > 96)
            {
                throw new ArgumentException(
                    "A transport ID between 1 and 96 characters is required.",
                    nameof(value));
            }

            return normalized;
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
