using System;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using UnityEditor.Build;

namespace Deucarian.TemplateViewerWeb.Editor
{
    /// <summary>
    /// One package-owned browser viewer workflow. A project definition selects
    /// the real product scene; installations without one retain the runnable
    /// generic sample workflow for backward compatibility.
    /// </summary>
    public sealed class WebViewerBuildManagerProvider :
        IDeucarianBuildManagerProvider
    {
        public const string DevelopmentProfilePath =
            WebViewerProductBuildWorkflow.DevelopmentProfilePath;
        public const string ProductionProfilePath =
            WebViewerProductBuildWorkflow.ProductionProfilePath;
        public const string DevelopmentScenePath =
            WebViewerFallbackBuildWorkflow.DevelopmentScenePath;
        public const string ProductionScenePath =
            WebViewerFallbackBuildWorkflow.ProductionScenePath;

        public string Id => TryReadDefinitionIdentity(
            definition => definition.ProviderId,
            "template-viewer-web");

        public string DisplayName => TryReadDefinitionIdentity(
            definition => definition.DisplayName,
            "Web Viewer Template");

        public int Order => 300;

        public bool CanSynchronize => true;

        public IReadOnlyList<DeucarianBuildManagerTarget> GetTargets()
        {
            WebViewerProductBuildConfiguration.TryLoad(
                out WebViewerProductBuildConfiguration configuration,
                out DeucarianBuildValidationResult validation);
            return SelectTargets(configuration, validation);
        }

        public void Synchronize()
        {
            if (WebViewerProductBuildConfiguration.TryLoad(
                    out WebViewerProductBuildConfiguration configuration,
                    out DeucarianBuildValidationResult validation))
            {
                WebViewerProductBuildWorkflow.Synchronize(configuration);
                return;
            }

            if (!validation.IsValid)
            {
                throw new BuildFailedException(validation.Format(
                    "Web Viewer product definition"));
            }

            WebViewerFallbackBuildWorkflow.Synchronize();
        }

        internal static IReadOnlyList<string> ValidateViewerComposition(
            IReadOnlyList<Deucarian.TemplateViewer.ViewerBootstrap> bootstraps,
            bool production) =>
            WebViewerSceneUtility.ValidateViewerComposition(
                bootstraps,
                production);

        internal static IReadOnlyList<DeucarianBuildManagerTarget>
            SelectTargets(
                WebViewerProductBuildConfiguration configuration,
                DeucarianBuildValidationResult validation)
        {
            if (configuration != null &&
                (validation == null || validation.IsValid))
            {
                return WebViewerProductBuildWorkflow.CreateTargets(
                    configuration);
            }

            if (validation != null && !validation.IsValid)
            {
                throw new BuildFailedException(validation.Format(
                    "Web Viewer product definition"));
            }

            return WebViewerFallbackBuildWorkflow.CreateTargets();
        }

        private static string TryReadDefinitionIdentity(
            Func<WebViewerProductBuildDefinition, string> selector,
            string fallback)
        {
            WebViewerProductBuildDefinition definition =
                UnityEditor.AssetDatabase.LoadAssetAtPath<
                    WebViewerProductBuildDefinition>(
                    WebViewerProductBuildConfiguration.DefinitionAssetPath);
            string value = definition == null ? null : selector(definition);
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}
