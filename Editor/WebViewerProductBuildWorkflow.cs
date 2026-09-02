using System;
using System.Collections.Generic;
using Deucarian.BuildPipeline;
using Deucarian.WebGLTemplate.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Editor
{
    internal static class WebViewerProductBuildWorkflow
    {
        internal const string DevelopmentProfilePath =
            "Assets/Deucarian/WebViewer/BuildProfiles/" +
            "WebViewer-Development.asset";
        internal const string ProductionProfilePath =
            "Assets/Deucarian/WebViewer/BuildProfiles/" +
            "WebViewer-Production.asset";

        internal static IReadOnlyList<DeucarianBuildManagerTarget>
            CreateTargets(WebViewerProductBuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return new[]
            {
                CreateTarget(
                    configuration,
                    DeucarianBuildEnvironment.Development),
                CreateTarget(
                    configuration,
                    DeucarianBuildEnvironment.Production)
            };
        }

        internal static void Synchronize(
            WebViewerProductBuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            DeucarianWebGLTemplate.Synchronize();
            WebViewerSceneUtility.ConfigureProductScene(configuration);
            SynchronizeProfile(
                configuration,
                DeucarianBuildEnvironment.Development);
            SynchronizeProfile(
                configuration,
                DeucarianBuildEnvironment.Production);
            WebViewerCommandHarnessCatalogGenerator.GenerateForScene(
                configuration.ScenePath);
            AssetDatabase.SaveAssets();
        }

        internal static string GetProfilePath(
            DeucarianBuildEnvironment environment) =>
            environment == DeucarianBuildEnvironment.Development
                ? DevelopmentProfilePath
                : ProductionProfilePath;

        private static DeucarianBuildManagerTarget CreateTarget(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment)
        {
            bool development =
                environment == DeucarianBuildEnvironment.Development;
            string id = development
                ? "web-development"
                : "web-production";
            string displayName = development
                ? "Web Development"
                : "Web Production";
            string profilePath = GetProfilePath(environment);
            string outputPath = configuration.GetOutputPath(environment);
            return new DeucarianBuildManagerTarget(
                id,
                displayName,
                "Builds the real product viewer scene through the shared " +
                "browser workflow.",
                profilePath,
                environment,
                outputPath,
                invocation => Build(
                    configuration,
                    environment,
                    invocation),
                () => Validate(configuration, environment),
                requireCompleteResult: true);
        }

        private static DeucarianBuildResult Build(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment,
            DeucarianBuildInvocation invocation)
        {
            if (invocation == null)
            {
                throw new ArgumentNullException(nameof(invocation));
            }

            DeucarianBuildRequest request = CreateRequest(
                environment,
                invocation);
            return Execute(
                configuration,
                environment,
                request,
                DeucarianBuildRunner.BuildWithOutputPreparation,
                DeucarianBuildRunner.Build);
        }

        internal static DeucarianBuildRequest CreateRequest(
            DeucarianBuildEnvironment environment,
            DeucarianBuildInvocation invocation)
        {
            if (invocation == null)
            {
                throw new ArgumentNullException(nameof(invocation));
            }

            return new DeucarianBuildRequest(
                invocation.BuildProfile,
                environment,
                invocation.OutputPath,
                invocation.AdditionalBuildOptions);
        }

        internal static DeucarianBuildResult Execute(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment,
            DeucarianBuildRequest request,
            Func<DeucarianBuildRequest, DeucarianBuildResult> preparedBuild,
            Func<DeucarianBuildRequest, DeucarianBuildResult> build)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            using (WebViewerProductBuildExecutionScope.Enter(configuration))
            {
                if (RequiresOutputPreparation(environment, request))
                {
                    if (preparedBuild == null)
                    {
                        throw new ArgumentNullException(nameof(preparedBuild));
                    }

                    return preparedBuild(request);
                }

                return build(request);
            }
        }

        internal static bool RequiresOutputPreparation(
            DeucarianBuildEnvironment environment,
            DeucarianBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return
                environment == DeucarianBuildEnvironment.Production ||
                (request.AdditionalBuildOptions &
                 BuildOptions.BuildScriptsOnly) != 0;
        }

        internal static DeucarianBuildValidationResult Validate(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment)
        {
            BuildProfile profile =
                WebViewerProductBuildConfiguration.LoadProfile(environment);
            return Validate(
                configuration,
                environment,
                profile,
                value => DeucarianWebGLTemplate.Validate(value),
                () => WebViewerSceneUtility.ValidateProductScene(
                    configuration,
                    environment == DeucarianBuildEnvironment.Production),
                WebViewerThemeBuildProcessor.Validate,
                DeucarianBuildOutputUtility.ValidatePreparation);
        }

        internal static DeucarianBuildValidationResult Validate(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment,
            BuildProfile profile,
            Func<BuildProfile, DeucarianBuildValidationResult>
                templateValidation,
            Func<DeucarianBuildValidationResult> sceneValidation,
            Func<DeucarianBuildValidationResult> themeValidation,
            Func<DeucarianBuildRequest, DeucarianBuildValidationResult>
                outputValidation)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var result = new DeucarianBuildValidationResult();
            if (profile == null)
            {
                result.Add(
                    "The shared Web Viewer " + environment +
                    " Build Profile is missing at '" +
                    GetProfilePath(environment) + "'. Synchronize the " +
                    "registered workflow.");
            }
            else
            {
                if (templateValidation == null)
                {
                    throw new ArgumentNullException(
                        nameof(templateValidation));
                }

                result.AddRange(templateValidation(profile).Issues);
                ValidateProfileScene(profile, configuration.ScenePath, result);
                result.AddRange(
                    DeucarianBuildProfileUtility.ValidatePlayerSettings(
                        profile,
                    CreatePlayerSettings(
                        configuration,
                        environment)).Issues);
            }

            if (environment == DeucarianBuildEnvironment.Production)
            {
                if (outputValidation == null)
                {
                    throw new ArgumentNullException(nameof(outputValidation));
                }

                DeucarianBuildValidationResult output = outputValidation(
                    new DeucarianBuildRequest(
                        profile,
                        environment,
                        configuration.GetOutputPath(environment)));
                if (output == null)
                {
                    result.Add(
                        "Production output validation returned no result.");
                }
                else
                {
                    result.AddRange(output.Issues);
                }
            }

            if (sceneValidation == null)
            {
                throw new ArgumentNullException(nameof(sceneValidation));
            }

            if (themeValidation == null)
            {
                throw new ArgumentNullException(nameof(themeValidation));
            }

            result.AddRange(sceneValidation().Issues);
            result.AddRange(themeValidation().Issues);
            return result;
        }

        private static void SynchronizeProfile(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment)
        {
            SynchronizeProfile(
                configuration,
                environment,
                GetProfilePath(environment));
        }

        internal static void SynchronizeProfile(
            WebViewerProductBuildConfiguration configuration,
            DeucarianBuildEnvironment environment,
            string profilePath)
        {
            BuildProfile profile = DeucarianBuildProfileUtility.CreateProfile(
                BuildTarget.WebGL,
                profilePath);
            DeucarianBuildProfileUtility.ApplySceneOverride(
                profile,
                new EditorBuildSettingsScene(
                    configuration.ScenePath,
                    true));
            profile = ReloadProfile(profilePath, "scene synchronization");
            DeucarianBuildRunner.ApplyPolicy(profile, environment);
            profile = ReloadProfile(profilePath, "build policy synchronization");
            DeucarianWebGLTemplate.ApplyTo(profile);
            profile = ReloadProfile(profilePath, "WebGL template synchronization");
            DeucarianBuildProfileUtility.ApplyPlayerSettings(
                profile,
                CreatePlayerSettings(configuration, environment));
        }

        internal static BuildProfile ReloadProfile(
            string profilePath,
            string operation)
        {
            BuildProfile profile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "The Web Viewer Build Profile became unavailable after " +
                    (string.IsNullOrWhiteSpace(operation)
                        ? "synchronization"
                        : operation.Trim()) +
                    " at '" + profilePath + "'.");
            }

            return profile;
        }

        private static DeucarianBuildProfilePlayerSettings
            CreatePlayerSettings(
                WebViewerProductBuildConfiguration configuration,
                DeucarianBuildEnvironment environment) =>
            new DeucarianBuildProfilePlayerSettings(
                configuration.GetBuildVersion(environment),
                runInBackground: true,
                environment == DeucarianBuildEnvironment.Development
                    ? InsecureHttpOption.DevelopmentOnly
                    : InsecureHttpOption.NotAllowed);

        private static void ValidateProfileScene(
            BuildProfile profile,
            string scenePath,
            DeucarianBuildValidationResult result)
        {
            EditorBuildSettingsScene[] scenes = profile.scenes;
            if (scenes == null ||
                scenes.Length != 1 ||
                !scenes[0].enabled ||
                !string.Equals(
                    scenes[0].path,
                    scenePath,
                    StringComparison.Ordinal))
            {
                result.Add(
                    "The shared Web Viewer Build Profile must contain only " +
                    "the enabled real product scene '" + scenePath + "'.");
            }
        }
    }
}
