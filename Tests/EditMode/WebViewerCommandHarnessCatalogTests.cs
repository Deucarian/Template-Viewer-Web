using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.Editor;
using Deucarian.TemplateViewer;
using Deucarian.TemplateViewer.Commands;
using Deucarian.TemplateViewerWeb.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerCommandHarnessCatalogTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GenericCatalogCoversEveryRegisteredCommand()
        {
            root = new GameObject("Harness Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();

            ViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            string[] registered = ViewerCommandHandlers.CreateDefault()
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] represented = catalog.Scenarios
                .Select(value => value.CommandName)
                .Distinct()
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(registered, represented);
            Assert.That(
                catalog.Scenarios.Count(value => value.RunAutomatically),
                Is.EqualTo(7));
            Assert.That(
                catalog.Scenarios.Single(
                    value => value.Id == "update-access-token")
                    .Payload.Value<string>("access_token"),
                Is.Empty);
        }

        [Test]
        public void CheckedInBrowserCatalogMatchesUnityGeneration()
        {
            root = new GameObject("Checked Browser Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            ViewerCommandHarnessCatalog generated =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(WebViewerBootstrap).Assembly);
            string path = Path.Combine(
                package.resolvedPath,
                "Browser~",
                "commands.generated.json");
            JToken checkedIn = JToken.Parse(File.ReadAllText(path));
            JToken expected = JToken.Parse(
                WebViewerCommandHarnessCatalogGenerator.Serialize(
                    generated,
                    WebViewerCommandHarnessCatalogGenerator
                        .DefaultTransportId));

            Assert.That(
                JToken.DeepEquals(checkedIn, expected),
                Is.True,
                "Regenerate Browser~/commands.generated.json from the Unity " +
                "command composition.");
        }

        [TestCase("activity-viewer")]
        [TestCase("report-viewer")]
        public void SerializedProductCatalogCarriesExactTransportId(
            string transportId)
        {
            root = new GameObject("Product Transport Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            ViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);

            JObject serialized = JObject.Parse(
                WebViewerCommandHarnessCatalogGenerator.Serialize(
                    catalog,
                    transportId));

            Assert.That(
                serialized.Value<string>("transport_id"),
                Is.EqualTo(transportId));
        }

        [Test]
        public void ProductFeatureReplacesVisibilityAndAddsItsOwnExamples()
        {
            root = new GameObject("Product Harness Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            root.AddComponent<HarnessFeature>();

            ViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            string[] commands = catalog.Scenarios
                .Select(value => value.CommandName)
                .Distinct()
                .ToArray();

            Assert.That(commands, Does.Contain("set_focus"));
            Assert.That(commands, Does.Not.Contain("select_elements"));
            Assert.That(commands, Does.Not.Contain("clear_selection"));
            ViewerCommandHarnessScenario scenario =
                catalog.Scenarios.Single(value => value.Id == "set-focus");
            Assert.That(scenario.RunAutomatically, Is.True);
            Assert.That(scenario.Payload.Value<long>("revision"), Is.EqualTo(4));
            Assert.That(catalog.DefaultScenarioId, Is.EqualTo("set-focus"));
            List<ViewerCommandHarnessScenario> orderedScenarios =
                catalog.Scenarios.ToList();
            Assert.That(
                orderedScenarios.IndexOf(scenario),
                Is.LessThan(orderedScenarios.FindIndex(
                    value => value.Id == "dispose")));
        }

        [Test]
        public void ExplicitRemoteFeatureUsesTheRuntimeCompositionCatalog()
        {
            root = new GameObject("Remote Product Harness Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            var featureObject = new GameObject("Remote Product Feature");
            featureObject.transform.SetParent(root.transform);
            HarnessFeature feature =
                featureObject.AddComponent<HarnessFeature>();
            var serialized = new UnityEditor.SerializedObject(bootstrap);
            UnityEditor.SerializedProperty explicitFeatures =
                serialized.FindProperty("explicitFeatureBehaviours");
            explicitFeatures.arraySize = 1;
            explicitFeatures.GetArrayElementAtIndex(0).objectReferenceValue =
                feature;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);

            Assert.That(
                catalog.Scenarios.Select(value => value.CommandName),
                Does.Contain("set_focus"));
            Assert.That(
                catalog.Scenarios.Select(value => value.CommandName),
                Does.Not.Contain("select_elements"));
        }

        [Test]
        public void CommandsWithoutExamplesRemainVisibleButAreNotAutomated()
        {
            var handlers = new[] { new HarnessCommandHandler("inspect_state") };
            ViewerCommandHarnessCatalog catalog =
                ViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    Array.Empty<ViewerCommandHarnessScenario>());

            Assert.That(catalog.Scenarios.Count, Is.EqualTo(1));
            Assert.That(catalog.Scenarios[0].CommandName, Is.EqualTo("inspect_state"));
            Assert.That(catalog.Scenarios[0].Label, Is.EqualTo("Inspect state"));
            Assert.That(catalog.Scenarios[0].RunAutomatically, Is.False);
        }

        [Test]
        public void RejectsMultipleDefaultExamples()
        {
            var handlers = new[] { new HarnessCommandHandler("set_focus") };
            var scenarios = new[]
            {
                new ViewerCommandHarnessScenario(
                    "first",
                    "First",
                    "set_focus",
                    isDefault: true),
                new ViewerCommandHarnessScenario(
                    "second",
                    "Second",
                    "set_focus",
                    isDefault: true)
            };

            Assert.Throws<InvalidOperationException>(() =>
                ViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    scenarios));
        }

        [Test]
        public void EditorTesterConsumesTheLiveViewerCatalog()
        {
            root = new GameObject("Live Tester Catalog");
            root.AddComponent<WebViewerBootstrap>();
            var source = new WebViewerCommandTestCatalogSource();

            Assert.That(
                source.TryGetCatalogJson(out string json, out string error),
                Is.True,
                error);
            Assert.That(
                CommandTestCatalog.TryParse(
                    json,
                    out CommandTestCatalog catalog,
                    out error),
                Is.True,
                error);
            Assert.That(
                catalog.Scenarios.Select(value => value.CommandName),
                Does.Contain("initialize_viewer"));
            Assert.That(catalog.RemoteEndpoint, Is.EqualTo("direct"));
        }

        public sealed class HarnessFeature :
            ViewerFeatureBehaviour,
            IViewerVisibilityFeatureFactory
        {
            public override IViewerVisibilityFeatureFactory
                VisibilityFeatureFactory => this;

            public override IReadOnlyList<ICommandHandler<ViewerApplication>>
                CreateCommandHandlers() =>
                    new[] { new HarnessCommandHandler("set_focus") };

            public override IReadOnlyList<ViewerCommandHarnessScenario>
                CreateCommandHarnessScenarios() =>
                    new[]
                    {
                        new ViewerCommandHarnessScenario(
                            "set-focus",
                            "Set focus",
                            "set_focus",
                            new JObject { ["revision"] = 4 },
                            isDefault: true)
                    };

            public bool TryCreate(
                ViewerModelContext context,
                out IViewerVisibilityFeature feature,
                out string error)
            {
                feature = null;
                error = "Not used by this catalog test.";
                return false;
            }
        }

        private sealed class HarnessCommandHandler :
            ICommandHandler<ViewerApplication>
        {
            private readonly IReadOnlyList<string> commandNames;

            public HarnessCommandHandler(string commandName)
            {
                commandNames = new[] { commandName };
            }

            public IReadOnlyList<string> CommandNames => commandNames;

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<ViewerApplication> context,
                CancellationToken cancellationToken) =>
                    Task.FromResult(CommandResult.Success());
        }
    }
}
