using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deucarian.BuildPipeline;
using Deucarian.TemplateViewerWeb.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerProductBuildDefinitionTests
    {
        private const string TestAssetFolder =
            "Assets/__DeucarianWebViewerProductBuildTests";
        private const string TestScenePath =
            TestAssetFolder + "/ProductViewer.unity";
        private const string TestHostScenePath =
            TestAssetFolder + "/Host.unity";
        private const string TestFeatureScriptPath =
            "Packages/com.deucarian.template.viewer.web/Tests/Support/" +
            "ProductBuildTestFeature.cs";

        private readonly List<UnityEngine.Object> objects =
            new List<UnityEngine.Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (EditorApplication.isCompiling)
            {
                yield return new RecompileScripts();
            }

            EnsureTestFolder();
            Scene hostScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.That(
                EditorSceneManager.SaveScene(hostScene, TestHostScenePath),
                Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }

            objects.Clear();
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.DeleteAsset(TestAssetFolder);
            }
        }

        [Test]
        public void ValidDefinitionCreatesOneOrderedProductWorkflow()
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out WebViewerProductBuildConfiguration configuration,
                out DeucarianBuildValidationResult validation);
            IReadOnlyList<DeucarianBuildManagerTarget> targets =
                WebViewerProductBuildWorkflow.CreateTargets(configuration);

            Assert.That(succeeded, Is.True, validation.Format("definition"));
            Assert.That(targets.Count, Is.EqualTo(2));
            Assert.That(targets[0].Id, Is.EqualTo("web-development"));
            Assert.That(
                targets[0].Environment,
                Is.EqualTo(DeucarianBuildEnvironment.Development));
            Assert.That(
                targets[0].BuildProfileAssetPath,
                Is.EqualTo(WebViewerBuildManagerProvider
                    .DevelopmentProfilePath));
            Assert.That(targets[0].OutputPath, Is.EqualTo(
                "Builds/Product/Development"));
            Assert.That(targets[0].RequireCompleteResult, Is.True);
            Assert.That(targets[1].Id, Is.EqualTo("web-production"));
            Assert.That(
                targets[1].Environment,
                Is.EqualTo(DeucarianBuildEnvironment.Production));
            Assert.That(
                targets[1].BuildProfileAssetPath,
                Is.EqualTo(WebViewerBuildManagerProvider
                    .ProductionProfilePath));
            Assert.That(targets[1].OutputPath, Is.EqualTo(
                "Builds/Product/Production"));
            Assert.That(targets[1].RequireCompleteResult, Is.True);
        }

        [Test]
        public void NoDefinitionFallbackContractRemainsRunnable()
        {
            IReadOnlyList<DeucarianBuildManagerTarget> targets =
                WebViewerFallbackBuildWorkflow.CreateTargets();

            Assert.That(targets.Count, Is.EqualTo(2));
            Assert.That(targets[0].Id, Is.EqualTo("development"));
            Assert.That(
                targets[0].BuildProfileAssetPath,
                Is.EqualTo(WebViewerBuildManagerProvider
                    .DevelopmentProfilePath));
            Assert.That(
                targets[0].OutputPath,
                Is.EqualTo("Builds/WebViewer-Development"));
            Assert.That(targets[0].RequireCompleteResult, Is.True);
            Assert.That(targets[1].Id, Is.EqualTo("production"));
            Assert.That(
                targets[1].BuildProfileAssetPath,
                Is.EqualTo(WebViewerBuildManagerProvider
                    .ProductionProfilePath));
            Assert.That(
                targets[1].OutputPath,
                Is.EqualTo("Builds/WebViewer-Production"));
            Assert.That(targets[1].RequireCompleteResult, Is.True);
            Assert.That(
                WebViewerBuildManagerProvider.DevelopmentScenePath,
                Is.EqualTo(WebViewerFallbackBuildWorkflow
                    .DevelopmentScenePath));
            Assert.That(
                WebViewerBuildManagerProvider.ProductionScenePath,
                Is.EqualTo(WebViewerFallbackBuildWorkflow
                    .ProductionScenePath));
        }

        [Test]
        public void DuplicateDefinitionsFailClosed()
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath,
                    "Assets/Product/AnotherDefinition.asset"
                },
                out _,
                out DeucarianBuildValidationResult validation);

            Assert.That(succeeded, Is.False);
            Assert.That(validation.Issues, Has.Some.Contains("duplicate"));
        }

        [Test]
        public void NonFeatureScriptFailsClosed()
        {
            WebViewerProductBuildDefinition definition = CreateValidDefinition();
            var root = new GameObject("Not A Domain Feature");
            objects.Add(root);
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            SetObject(
                definition,
                "requiredDomainFeatureScript",
                MonoScript.FromMonoBehaviour(bootstrap));

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out _,
                out DeucarianBuildValidationResult validation);

            Assert.That(succeeded, Is.False);
            Assert.That(validation.Issues, Has.Some.Contains(
                "ViewerFeatureBehaviour"));
        }

        [TestCase(96, true)]
        [TestCase(97, false)]
        public void TransportIdEnforcesRuntimeLengthBoundary(
            int length,
            bool expectedValid)
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            SetString(definition, "transportId", new string('a', length));

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out _,
                out DeucarianBuildValidationResult validation);

            Assert.That(
                succeeded,
                Is.EqualTo(expectedValid),
                validation.Format("definition"));
            if (!expectedValid)
            {
                Assert.That(validation.Issues, Has.Some.Contains("96"));
            }
        }

        [Test]
        public void OutputPathTraversalFailsClosed()
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            SetString(
                definition,
                "productionOutputPath",
                "../Outside/Production");

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out _,
                out DeucarianBuildValidationResult validation);

            Assert.That(succeeded, Is.False);
            Assert.That(validation.Issues, Has.Some.Contains(
                "child of Builds"));
        }

        [Test]
        public void WindowsAuthoredOutputPathNormalizesForEveryHostPlatform()
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            SetString(
                definition,
                "developmentOutputPath",
                @"Builds\Product\Development");

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out WebViewerProductBuildConfiguration configuration,
                out DeucarianBuildValidationResult validation);

            Assert.That(succeeded, Is.True, validation.Format("definition"));
            Assert.That(
                configuration.GetOutputPath(
                    DeucarianBuildEnvironment.Development),
                Is.EqualTo("Builds/Product/Development"));
        }

        [TestCase(".")]
        [TestCase("Assets/WebViewer")]
        [TestCase("Builds")]
        [TestCase("Builds/")]
        [TestCase("Builds/.")]
        [TestCase("Builds/Product/..")]
        [TestCase("Builds//Product")]
        public void OutputPathOutsideBuildsChildFailsClosed(string path)
        {
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            SetString(definition, "developmentOutputPath", path);

            bool succeeded = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                "Assets/Product/Viewer.unity",
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out _,
                out DeucarianBuildValidationResult validation);

            Assert.That(succeeded, Is.False);
            Assert.That(validation.Issues, Has.Some.Contains(
                "child of Builds"));
        }

        [Test]
        public void DefinitionAllowsOnlyDeclarativeProductDifferences()
        {
            string[] serializedFields = typeof(
                    WebViewerProductBuildDefinition)
                .GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(serializedFields, Is.EqualTo(new[]
            {
                "developmentBuildVersion",
                "developmentOutputPath",
                "displayName",
                "productionBuildVersion",
                "productionOutputPath",
                "providerId",
                "requiredDomainFeatureScript",
                "transportId",
                "viewerScene"
            }));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public void RealSceneRequiresExactlyOneDeclaredDomainFeature(
            int featureCount,
            bool expectedValid)
        {
            CreateProductScene(featureCount);
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            bool created = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                TestScenePath,
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out WebViewerProductBuildConfiguration configuration,
                out DeucarianBuildValidationResult definitionValidation);

            Assert.That(
                created,
                Is.True,
                definitionValidation.Format("definition"));
            DeucarianBuildValidationResult sceneValidation =
                WebViewerSceneUtility.ValidateProductScene(
                    configuration,
                    production: false);

            Assert.That(
                sceneValidation.IsValid,
                Is.EqualTo(expectedValid),
                sceneValidation.Format("scene"));
            if (!expectedValid)
            {
                Assert.That(
                    sceneValidation.Issues,
                    Has.Some.Contains("exactly one"));
            }
        }

        [Test]
        public void UnassignedRemoteDomainFeatureFailsClosed()
        {
            CreateProductScene(
                featureCount: 1,
                attachBesideBootstrap: false);
            WebViewerProductBuildDefinition definition =
                CreateValidDefinition();
            Assert.That(
                WebViewerProductBuildConfiguration.TryCreate(
                    definition,
                    TestScenePath,
                    new[]
                    {
                        WebViewerProductBuildConfiguration.DefinitionAssetPath
                    },
                    out WebViewerProductBuildConfiguration configuration,
                    out DeucarianBuildValidationResult definitionValidation),
                Is.True,
                definitionValidation.Format("definition"));

            DeucarianBuildValidationResult sceneValidation =
                WebViewerSceneUtility.ValidateProductScene(
                    configuration,
                    production: false);

            Assert.That(sceneValidation.IsValid, Is.False);
            Assert.That(
                sceneValidation.Issues,
                Has.Some.Contains("resolve exactly"));
        }

        private WebViewerProductBuildDefinition CreateValidDefinition()
        {
            var definition = ScriptableObject.CreateInstance<
                WebViewerProductBuildDefinition>();
            objects.Add(definition);
            SetString(definition, "providerId", "product-viewer");
            SetString(definition, "displayName", "Product Viewer");
            SetString(definition, "transportId", "product-viewer");
            SetString(
                definition,
                "developmentBuildVersion",
                "local-product-viewer");
            SetString(definition, "productionBuildVersion", "1.0");
            SetString(
                definition,
                "developmentOutputPath",
                "Builds/Product/Development");
            SetString(
                definition,
                "productionOutputPath",
                "Builds/Product/Production");

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                TestFeatureScriptPath);
            Assert.That(script, Is.Not.Null);
            Assert.That(script.GetClass(), Is.EqualTo(
                typeof(ProductBuildTestFeature)));
            SetObject(definition, "requiredDomainFeatureScript", script);
            return definition;
        }

        private static void SetString(
            WebViewerProductBuildDefinition definition,
            string field,
            string value)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(
            WebViewerProductBuildDefinition definition,
            string field,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateProductScene(
            int featureCount,
            bool attachBesideBootstrap = true)
        {
            EnsureTestFolder();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                var bootstrapObject = new GameObject("Web Viewer Bootstrap");
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                WebViewerBootstrap bootstrap =
                    bootstrapObject.AddComponent<WebViewerBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("iframeMode").boolValue = false;
                serialized.FindProperty("parentOrigin").stringValue =
                    string.Empty;
                serialized.FindProperty("transportId").stringValue =
                    "product-viewer";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                for (int i = 0; i < featureCount; i++)
                {
                    GameObject featureObject = attachBesideBootstrap
                        ? bootstrapObject
                        : new GameObject(
                            "Product Domain Feature " + (i + 1));
                    if (!attachBesideBootstrap)
                    {
                        SceneManager.MoveGameObjectToScene(
                            featureObject,
                            scene);
                    }

                    featureObject.AddComponent<ProductBuildTestFeature>();
                }

                EditorSceneManager.SaveScene(scene, TestScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureTestFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "__DeucarianWebViewerProductBuildTests");
            }
        }
    }
}
