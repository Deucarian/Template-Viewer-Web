using System;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using UnityEditor.Build;

namespace Deucarian.TemplateViewerWeb.Editor
{
    /// <summary>
    /// Stable CI and compatibility entry points for the canonical product
    /// definition. Product projects never need to reimplement build behavior.
    /// </summary>
    public static class WebViewerProductBuildApi
    {
        public const string DefinitionAssetPath =
            WebViewerProductBuildConfiguration.DefinitionAssetPath;

        public static void Synchronize()
        {
            WebViewerProductBuildConfiguration configuration =
                RequireConfiguration();
            WebViewerProductBuildWorkflow.Synchronize(configuration);
        }

        public static DeucarianBuildResult BuildDevelopment() =>
            Build(DeucarianBuildEnvironment.Development);

        public static DeucarianBuildResult BuildProduction() =>
            Build(DeucarianBuildEnvironment.Production);

        public static DeucarianBuildManagerTarget GetTarget(
            DeucarianBuildEnvironment environment)
        {
            IReadOnlyList<DeucarianBuildManagerTarget> targets =
                WebViewerProductBuildWorkflow.CreateTargets(
                    RequireConfiguration());
            int index = environment ==
                        DeucarianBuildEnvironment.Development
                ? 0
                : 1;
            return targets[index];
        }

        private static DeucarianBuildResult Build(
            DeucarianBuildEnvironment environment) =>
            DeucarianBuildDispatcher.BuildDefault(
                GetTarget(environment),
                DeucarianBuildInvocationSource.CommandLine);

        private static WebViewerProductBuildConfiguration
            RequireConfiguration()
        {
            if (WebViewerProductBuildConfiguration.TryLoad(
                    out WebViewerProductBuildConfiguration configuration,
                    out DeucarianBuildValidationResult validation))
            {
                return configuration;
            }

            if (validation.IsValid)
            {
                validation.Add(
                    "No Web Viewer product build definition exists at '" +
                    DefinitionAssetPath + "'.");
            }

            throw new BuildFailedException(validation.Format(
                "Web Viewer product definition"));
        }
    }
}
