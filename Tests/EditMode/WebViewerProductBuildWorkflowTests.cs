using System;
using System.Collections;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using Deucarian.TemplateViewerWeb.Editor;
using Deucarian.WebGLTemplate.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerProductBuildWorkflowTests
    {
        private const string TestAssetFolder =
            "Assets/__DeucarianWebViewerWorkflowTests";
        private const string TestScenePath =
            TestAssetFolder + "/ProductViewer.unity";
        private const string TestFeatureScriptPath =
            "Packages/com.deucarian.template.viewer.web/Tests/Support/" +
            "ProductBuildTestFeature.cs";
        private const string TestDevelopmentProfilePath =
            TestAssetFolder + "/WebViewer-Development.asset";
        private const string TestProductionProfilePath =
            TestAssetFolder + "/WebViewer-Production.asset";
        private const string TestHostScenePath =
            TestAssetFolder + "/Host.unity";

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
        public void ActualInvocationAndRequiredOutputPreparationArePreserved()
        {
            WebViewerProductBuildConfiguration configuration =
                CreateConfiguration();
            EnsureTestFolder();
            BuildProfile profile = DeucarianBuildProfileUtility.CreateProfile(
                BuildTarget.WebGL,
                TestDevelopmentProfilePath);
            const BuildOptions options =
                BuildOptions.BuildScriptsOnly | BuildOptions.AutoRunPlayer;
            var invocation = new DeucarianBuildInvocation(
                profile,
                "D:/External/WebViewer",
                options,
                DeucarianBuildInvocationSource.UnityBuildProfiles);

            DeucarianBuildRequest developmentRequest =
                WebViewerProductBuildWorkflow.CreateRequest(
                    DeucarianBuildEnvironment.Development,
                    invocation);
            Assert.That(developmentRequest.BuildProfile, Is.SameAs(profile));
            Assert.That(
                developmentRequest.Environment,
                Is.EqualTo(DeucarianBuildEnvironment.Development));
            Assert.That(
                developmentRequest.OutputPath,
                Is.EqualTo("D:/External/WebViewer"));
            Assert.That(
                developmentRequest.AdditionalBuildOptions,
                Is.EqualTo(options));

            var order = new List<string>();
            var expected = new DeucarianBuildResult();
            DeucarianBuildResult actual =
                WebViewerProductBuildWorkflow.Execute(
                    configuration,
                    DeucarianBuildEnvironment.Development,
                    developmentRequest,
                    request =>
                    {
                        order.Add("prepared-build");
                        Assert.That(request, Is.SameAs(developmentRequest));
                        Assert.That(
                            WebViewerProductBuildExecutionScope.Current,
                            Is.SameAs(configuration));
                        return expected;
                    },
                    _ => throw new AssertionException(
                        "Scripts-only builds must use lifecycle-aware preparation."));

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(order, Is.EqualTo(new[] { "prepared-build" }));
            Assert.That(
                WebViewerProductBuildExecutionScope.Current,
                Is.Null);

            order.Clear();
            var ordinaryDevelopmentRequest = new DeucarianBuildRequest(
                profile,
                DeucarianBuildEnvironment.Development,
                "Builds/Product/Development",
                BuildOptions.AutoRunPlayer);
            WebViewerProductBuildWorkflow.Execute(
                configuration,
                DeucarianBuildEnvironment.Development,
                ordinaryDevelopmentRequest,
                _ => throw new AssertionException(
                    "Ordinary Development builds must preserve their output."),
                _ =>
                {
                    order.Add("build");
                    return expected;
                });
            Assert.That(order, Is.EqualTo(new[] { "build" }));

            order.Clear();
            DeucarianBuildRequest productionRequest =
                WebViewerProductBuildWorkflow.CreateRequest(
                    DeucarianBuildEnvironment.Production,
                    invocation);
            WebViewerProductBuildWorkflow.Execute(
                configuration,
                DeucarianBuildEnvironment.Production,
                productionRequest,
                request =>
                {
                    Assert.That(request, Is.SameAs(productionRequest));
                    order.Add("prepared-build");
                    return expected;
                },
                _ => throw new AssertionException(
                    "Production builds must use lifecycle-aware preparation."));

            Assert.That(order, Is.EqualTo(new[] { "prepared-build" }));
        }

        [Test]
        public void FallbackScriptsOnlyBuildCannotBypassOutputCompatibility()
        {
            EnsureTestFolder();
            BuildProfile profile = DeucarianBuildProfileUtility.CreateProfile(
                BuildTarget.WebGL,
                TestDevelopmentProfilePath);
            var request = new DeucarianBuildRequest(
                profile,
                DeucarianBuildEnvironment.Development,
                "Builds/WebViewer-Development",
                BuildOptions.BuildScriptsOnly);
            var order = new List<string>();
            var expected = new DeucarianBuildResult();

            DeucarianBuildResult actual =
                WebViewerFallbackBuildWorkflow.Execute(
                    DeucarianBuildEnvironment.Development,
                    request,
                    _ =>
                    {
                        order.Add("prepared-build");
                        return expected;
                    },
                    _ => throw new AssertionException(
                        "Fallback scripts-only builds must use lifecycle-aware preparation."));

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(order, Is.EqualTo(new[] { "prepared-build" }));
        }

        [Test]
        public void MissingProfileStillAggregatesSceneAndThemeValidation()
        {
            WebViewerProductBuildConfiguration configuration =
                CreateConfiguration();
            var scene = new DeucarianBuildValidationResult();
            scene.Add("scene blocker");
            var theme = new DeucarianBuildValidationResult();
            theme.Add("theme blocker");
            bool templateCalled = false;

            DeucarianBuildValidationResult validation =
                WebViewerProductBuildWorkflow.Validate(
                    configuration,
                    DeucarianBuildEnvironment.Development,
                    null,
                    _ =>
                    {
                        templateCalled = true;
                        return new DeucarianBuildValidationResult();
                    },
                    () => scene,
                    () => theme,
                    _ => new DeucarianBuildValidationResult());

            Assert.That(templateCalled, Is.False);
            Assert.That(validation.Issues, Has.Some.Contains("missing"));
            Assert.That(validation.Issues, Has.Some.Contains("scene blocker"));
            Assert.That(validation.Issues, Has.Some.Contains("theme blocker"));
        }

        [Test]
        public void PreparationValidationRunsOnlyForProductionAndAggregates()
        {
            WebViewerProductBuildConfiguration configuration =
                CreateConfiguration();
            int outputValidations = 0;
            Func<DeucarianBuildRequest, DeucarianBuildValidationResult>
                validateOutput = request =>
                {
                    outputValidations++;
                    Assert.That(
                        request.Environment,
                        Is.EqualTo(DeucarianBuildEnvironment.Production));
                    Assert.That(
                        request.OutputPath,
                        Is.EqualTo("Builds/Product/Production"));
                    var output = new DeucarianBuildValidationResult();
                    output.Add("unsafe production output");
                    return output;
                };

            DeucarianBuildValidationResult development =
                WebViewerProductBuildWorkflow.Validate(
                    configuration,
                    DeucarianBuildEnvironment.Development,
                    null,
                    _ => new DeucarianBuildValidationResult(),
                    () => new DeucarianBuildValidationResult(),
                    () => new DeucarianBuildValidationResult(),
                    validateOutput);
            Assert.That(outputValidations, Is.Zero);
            Assert.That(
                development.Issues,
                Has.None.Contains("unsafe production output"));

            DeucarianBuildValidationResult production =
                WebViewerProductBuildWorkflow.Validate(
                    configuration,
                    DeucarianBuildEnvironment.Production,
                    null,
                    _ => new DeucarianBuildValidationResult(),
                    () => new DeucarianBuildValidationResult(),
                    () => new DeucarianBuildValidationResult(),
                    validateOutput);

            Assert.That(outputValidations, Is.EqualTo(1));
            Assert.That(
                production.Issues,
                Has.Some.Contains("unsafe production output"));
        }

        [Test]
        public void ProviderRoutesProductFallbackAndInvalidDefinitions()
        {
            WebViewerProductBuildConfiguration configuration =
                CreateConfiguration();
            var valid = new DeucarianBuildValidationResult();

            IReadOnlyList<DeucarianBuildManagerTarget> product =
                WebViewerBuildManagerProvider.SelectTargets(
                    configuration,
                    valid);
            IReadOnlyList<DeucarianBuildManagerTarget> fallback =
                WebViewerBuildManagerProvider.SelectTargets(null, valid);

            Assert.That(
                new[] { product[0].Id, product[1].Id },
                Is.EqualTo(new[] { "web-development", "web-production" }));
            Assert.That(
                new[] { fallback[0].Id, fallback[1].Id },
                Is.EqualTo(new[] { "development", "production" }));

            var invalid = new DeucarianBuildValidationResult();
            invalid.Add("duplicate product definition");
            Assert.Throws<BuildFailedException>(() =>
                WebViewerBuildManagerProvider.SelectTargets(
                    configuration,
                    invalid));
        }

        [TestCase(
            DeucarianBuildEnvironment.Development,
            "local-product-viewer",
            InsecureHttpOption.DevelopmentOnly)]
        [TestCase(
            DeucarianBuildEnvironment.Production,
            "1.0",
            InsecureHttpOption.NotAllowed)]
        public void ProfileSynchronizationIsExactAndValidationIsPassive(
            DeucarianBuildEnvironment environment,
            string expectedVersion,
            InsecureHttpOption expectedHttp)
        {
            CreateProductScene();
            WebViewerProductBuildConfiguration configuration =
                CreateConfiguration();
            string profilePath = environment ==
                                 DeucarianBuildEnvironment.Development
                ? TestDevelopmentProfilePath
                : TestProductionProfilePath;
            BuildProfile activeBefore = BuildProfile.GetActiveBuildProfile();

            WebViewerProductBuildWorkflow.SynchronizeProfile(
                configuration,
                environment,
                profilePath);

            Assert.That(
                BuildProfile.GetActiveBuildProfile(),
                Is.SameAs(activeBefore));
            BuildProfile profile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.scenes, Has.Length.EqualTo(1));
            Assert.That(profile.scenes[0].enabled, Is.True);
            Assert.That(profile.scenes[0].path, Is.EqualTo(TestScenePath));
            Assert.That(
                DeucarianBuildRunner.GetPolicy(profile)
                    .ValidateProfile(profile, environment).IsValid,
                Is.True);
            Assert.That(
                ReadProfileSetting(profile, "webGLTemplate"),
                Is.EqualTo(DeucarianWebGLTemplate.PlayerSettingsValue));

            var expected = new DeucarianBuildProfilePlayerSettings(
                expectedVersion,
                runInBackground: true,
                expectedHttp);
            BuildProfile activeBeforeValidation =
                BuildProfile.GetActiveBuildProfile();
            DeucarianBuildValidationResult playerSettings =
                DeucarianBuildProfileUtility.ValidatePlayerSettings(
                    profile,
                    expected);

            Assert.That(
                playerSettings.IsValid,
                Is.True,
                playerSettings.Format("player settings"));
            Assert.That(
                BuildProfile.GetActiveBuildProfile(),
                Is.SameAs(activeBeforeValidation));
        }

        [TestCase(DeucarianBuildEnvironment.Development)]
        [TestCase(DeucarianBuildEnvironment.Production)]
        public void FallbackProfileSynchronizationSurvivesAssetInvalidation(
            DeucarianBuildEnvironment environment)
        {
            CreateProductScene();
            string profilePath = environment ==
                                 DeucarianBuildEnvironment.Development
                ? TestDevelopmentProfilePath
                : TestProductionProfilePath;
            BuildProfile activeBefore = BuildProfile.GetActiveBuildProfile();

            WebViewerFallbackBuildWorkflow.SynchronizeProfile(
                profilePath,
                TestScenePath,
                environment);

            Assert.That(
                BuildProfile.GetActiveBuildProfile(),
                Is.SameAs(activeBefore));
            BuildProfile profile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.scenes, Has.Length.EqualTo(1));
            Assert.That(profile.scenes[0].enabled, Is.True);
            Assert.That(profile.scenes[0].path, Is.EqualTo(TestScenePath));
            Assert.That(
                DeucarianBuildRunner.GetPolicy(profile)
                    .ValidateProfile(profile, environment).IsValid,
                Is.True);
            Assert.That(
                ReadProfileSetting(profile, "webGLTemplate"),
                Is.EqualTo(DeucarianWebGLTemplate.PlayerSettingsValue));
        }

        private WebViewerProductBuildConfiguration CreateConfiguration()
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

            MonoScript featureScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(
                    TestFeatureScriptPath);
            Assert.That(featureScript, Is.Not.Null);
            Assert.That(featureScript.GetClass(), Is.EqualTo(
                typeof(ProductBuildTestFeature)));
            SetObject(
                definition,
                "requiredDomainFeatureScript",
                featureScript);

            bool created = WebViewerProductBuildConfiguration.TryCreate(
                definition,
                TestScenePath,
                new[]
                {
                    WebViewerProductBuildConfiguration.DefinitionAssetPath
                },
                out WebViewerProductBuildConfiguration configuration,
                out DeucarianBuildValidationResult validation);
            Assert.That(created, Is.True, validation.Format("definition"));
            return configuration;
        }

        private static void CreateProductScene()
        {
            EnsureTestFolder();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Web Viewer Bootstrap");
                SceneManager.MoveGameObjectToScene(root, scene);
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                root.AddComponent<ProductBuildTestFeature>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("iframeMode").boolValue = false;
                serialized.FindProperty("parentOrigin").stringValue =
                    string.Empty;
                serialized.FindProperty("transportId").stringValue =
                    "product-viewer";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, TestScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string ReadProfileSetting(
            BuildProfile profile,
            string key)
        {
            var serialized = new SerializedObject(profile);
            SerializedProperty settings = serialized.FindProperty(
                "m_PlayerSettingsYaml.m_Settings");
            Assert.That(settings, Is.Not.Null);
            for (int i = 0; i < settings.arraySize; i++)
            {
                SerializedProperty lineProperty = settings
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("line");
                string line = lineProperty?.stringValue ?? string.Empty;
                if (line.StartsWith("| ", StringComparison.Ordinal))
                {
                    line = line.Substring(2);
                }

                string content = line.Trim();
                string prefix = key + ":";
                if (content.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return content.Substring(prefix.Length).Trim();
                }
            }

            Assert.Fail("Build Profile setting was not serialized: " + key);
            return string.Empty;
        }

        private static void EnsureTestFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestAssetFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "__DeucarianWebViewerWorkflowTests");
            }
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
    }
}
