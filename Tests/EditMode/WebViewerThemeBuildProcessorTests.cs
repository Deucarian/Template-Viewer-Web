using System;
using System.IO;
using System.Linq;
using Deucarian.BuildPipeline;
using Deucarian.TemplateViewerWeb.Editor;
using Deucarian.Theming;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerThemeBuildProcessorTests
    {
        [Test]
        public void ReferenceThemeCreatesOneSafeFirstPaintAssignment()
        {
            DeucarianTheme theme =
                WebViewerThemeBuildProcessor.LoadAndValidateDefaultTheme();

            string script =
                WebViewerThemeBuildProcessor.CreateGeneratedThemeScript(
                    theme);

            Assert.That(
                script,
                Does.StartWith(
                    "window.deucarianWebGLInitialTheme = {"));
            Assert.That(script, Does.EndWith(";\n"));
            Assert.That(script, Does.Not.Contain("</"));
            Assert.That(script, Does.Not.Contain("token"));
            Assert.That(script, Does.Not.Contain("authorization"));
        }

        [Test]
        public void ProductValidationAlwaysChecksTmpReadiness()
        {
            DeucarianBuildValidationResult validation =
                WebViewerThemeBuildProcessor.Validate();

            Assert.That(validation, Is.Not.Null);
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                Assert.That(
                    validation.Issues.Any(issue =>
                        issue.Contains("TextMesh Pro")),
                    Is.True);
            }
        }

        [Test]
        public void GeneratedThemeIsWrittenInsideBuildOutput()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "Deucarian-Web-Theme-" + Guid.NewGuid().ToString("N"));
            try
            {
                string written =
                    WebViewerThemeBuildProcessor.WriteGeneratedThemeFile(
                        root);

                Assert.That(File.Exists(written), Is.True);
                Assert.That(
                    Path.GetFullPath(written),
                    Is.EqualTo(Path.GetFullPath(Path.Combine(
                        root,
                        WebViewerThemeBuildProcessor
                            .GeneratedThemeRelativePath))));
                Assert.That(
                    File.ReadAllText(written),
                    Does.StartWith(
                        "window.deucarianWebGLInitialTheme = {"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void ExecutionScopeRestoresNestedProductDefinition()
        {
            var first = UnityEngine.ScriptableObject.CreateInstance<
                WebViewerProductBuildDefinition>();
            var second = UnityEngine.ScriptableObject.CreateInstance<
                WebViewerProductBuildDefinition>();
            try
            {
                WebViewerProductBuildConfiguration.TryCreate(
                    first,
                    "Assets/First.unity",
                    new[]
                    {
                        WebViewerProductBuildConfiguration.DefinitionAssetPath
                    },
                    out WebViewerProductBuildConfiguration firstConfig,
                    out _);
                WebViewerProductBuildConfiguration.TryCreate(
                    second,
                    "Assets/Second.unity",
                    new[]
                    {
                        WebViewerProductBuildConfiguration.DefinitionAssetPath
                    },
                    out WebViewerProductBuildConfiguration secondConfig,
                    out _);

                using (WebViewerProductBuildExecutionScope.Enter(firstConfig))
                {
                    Assert.That(
                        WebViewerProductBuildExecutionScope.Current,
                        Is.SameAs(firstConfig));
                    using (WebViewerProductBuildExecutionScope.Enter(
                               secondConfig))
                    {
                        Assert.That(
                            WebViewerProductBuildExecutionScope.Current,
                            Is.SameAs(secondConfig));
                    }

                    Assert.That(
                        WebViewerProductBuildExecutionScope.Current,
                        Is.SameAs(firstConfig));
                }

                Assert.That(
                    WebViewerProductBuildExecutionScope.Current,
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ProcessorRunsOnlyForScopedProductWebGlBuilds()
        {
            var definition = ScriptableObject.CreateInstance<
                WebViewerProductBuildDefinition>();
            try
            {
                WebViewerProductBuildConfiguration.TryCreate(
                    definition,
                    "Assets/Product.unity",
                    new[]
                    {
                        WebViewerProductBuildConfiguration.DefinitionAssetPath
                    },
                    out WebViewerProductBuildConfiguration configuration,
                    out _);
                Assert.That(configuration, Is.Not.Null);
                int validations = 0;
                int writes = 0;
                string writtenPath = null;
                Func<DeucarianBuildValidationResult> validate = () =>
                {
                    validations++;
                    return new DeucarianBuildValidationResult();
                };
                Action<string> write = path =>
                {
                    writes++;
                    writtenPath = path;
                };

                WebViewerThemeBuildProcessor.ProcessPrebuild(
                    BuildTarget.WebGL,
                    null,
                    validate);
                WebViewerThemeBuildProcessor.ProcessPostbuild(
                    BuildTarget.WebGL,
                    null,
                    "Builds/Fallback",
                    write);
                WebViewerThemeBuildProcessor.ProcessPrebuild(
                    BuildTarget.StandaloneWindows64,
                    configuration,
                    validate);
                WebViewerThemeBuildProcessor.ProcessPostbuild(
                    BuildTarget.StandaloneWindows64,
                    configuration,
                    "Builds/Desktop",
                    write);

                Assert.That(validations, Is.Zero);
                Assert.That(writes, Is.Zero);

                WebViewerThemeBuildProcessor.ProcessPrebuild(
                    BuildTarget.WebGL,
                    configuration,
                    validate);
                WebViewerThemeBuildProcessor.ProcessPostbuild(
                    BuildTarget.WebGL,
                    configuration,
                    "Builds/Product",
                    write);

                Assert.That(validations, Is.EqualTo(1));
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(writtenPath, Is.EqualTo("Builds/Product"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ScopedPreprocessorFailsClosedOnPresentationIssue()
        {
            var definition = ScriptableObject.CreateInstance<
                WebViewerProductBuildDefinition>();
            try
            {
                WebViewerProductBuildConfiguration.TryCreate(
                    definition,
                    "Assets/Product.unity",
                    new[]
                    {
                        WebViewerProductBuildConfiguration.DefinitionAssetPath
                    },
                    out WebViewerProductBuildConfiguration configuration,
                    out _);
                Assert.That(configuration, Is.Not.Null);
                var invalid = new DeucarianBuildValidationResult();
                invalid.Add("presentation blocker");

                BuildFailedException failure =
                    Assert.Throws<BuildFailedException>(() =>
                        WebViewerThemeBuildProcessor.ProcessPrebuild(
                            BuildTarget.WebGL,
                            configuration,
                            () => invalid));

                Assert.That(failure.Message, Does.Contain(
                    "presentation blocker"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
    }
}
