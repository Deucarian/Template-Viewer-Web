using System;
using System.Collections.Generic;
using System.IO;
using Deucarian.BuildPipeline;
using Deucarian.TemplateViewer;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Deucarian.TemplateViewerWeb.Editor
{
    internal sealed class WebViewerProductBuildConfiguration
    {
        private const int MaximumTransportIdLength = 96;

        internal const string DefinitionAssetPath =
            "Assets/Deucarian/WebViewer/Editor/" +
            "WebViewerProductBuildDefinition.asset";

        private WebViewerProductBuildConfiguration(
            WebViewerProductBuildDefinition definition,
            string scenePath,
            Type requiredDomainFeatureType)
        {
            Definition = definition;
            ScenePath = scenePath;
            RequiredDomainFeatureType = requiredDomainFeatureType;
        }

        internal WebViewerProductBuildDefinition Definition { get; }

        internal string ScenePath { get; }

        internal Type RequiredDomainFeatureType { get; }

        internal string ProviderId => Definition.ProviderId.Trim();

        internal string DisplayName => Definition.DisplayName.Trim();

        internal string TransportId => Definition.TransportId.Trim();

        internal string GetBuildVersion(
            DeucarianBuildEnvironment environment) =>
            (environment == DeucarianBuildEnvironment.Development
                ? Definition.DevelopmentBuildVersion
                : Definition.ProductionBuildVersion).Trim();

        internal string GetOutputPath(
            DeucarianBuildEnvironment environment) =>
            Normalize(environment == DeucarianBuildEnvironment.Development
                ? Definition.DevelopmentOutputPath
                : Definition.ProductionOutputPath);

        internal static bool TryLoad(
            out WebViewerProductBuildConfiguration configuration,
            out DeucarianBuildValidationResult validation)
        {
            WebViewerProductBuildDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    WebViewerProductBuildDefinition>(DefinitionAssetPath);
            string[] allDefinitions = AssetDatabase.FindAssets(
                "t:WebViewerProductBuildDefinition");
            if (definition == null)
            {
                validation = new DeucarianBuildValidationResult();
                configuration = null;
                if (allDefinitions.Length > 0)
                {
                    validation.Add(
                        "Move the Web Viewer product build definition to '" +
                        DefinitionAssetPath + "'.");
                }

                return false;
            }

            var definitionPaths = new List<string>();
            for (int i = 0; i < allDefinitions.Length; i++)
            {
                definitionPaths.Add(
                    AssetDatabase.GUIDToAssetPath(allDefinitions[i]));
            }

            return TryCreate(
                definition,
                AssetDatabase.GetAssetPath(definition.ViewerScene),
                definitionPaths,
                out configuration,
                out validation);
        }

        internal static bool TryCreate(
            WebViewerProductBuildDefinition definition,
            string scenePath,
            IReadOnlyList<string> definitionPaths,
            out WebViewerProductBuildConfiguration configuration,
            out DeucarianBuildValidationResult validation)
        {
            validation = new DeucarianBuildValidationResult();
            configuration = null;
            if (definition == null)
            {
                validation.Add(
                    "A Web Viewer product build definition is required.");
                return false;
            }

            if (definitionPaths != null)
            {
                for (int i = 0; i < definitionPaths.Count; i++)
                {
                    string path = definitionPaths[i];
                    if (!string.Equals(
                            Normalize(path),
                            DefinitionAssetPath,
                            StringComparison.Ordinal))
                    {
                        validation.Add(
                            "Remove the duplicate Web Viewer product build " +
                            "definition at '" + path + "'.");
                    }
                }
            }

            ValidateIdentifier(
                definition.ProviderId,
                "provider ID",
                validation);
            ValidateText(definition.DisplayName, "display name", validation);
            ValidateIdentifier(
                definition.TransportId,
                "transport ID",
                validation,
                MaximumTransportIdLength);
            ValidateText(
                definition.DevelopmentBuildVersion,
                "development build version",
                validation);
            ValidateText(
                definition.ProductionBuildVersion,
                "production build version",
                validation);
            ValidateOutputPath(
                definition.DevelopmentOutputPath,
                "development output path",
                validation);
            ValidateOutputPath(
                definition.ProductionOutputPath,
                "production output path",
                validation);
            if (string.IsNullOrWhiteSpace(scenePath) ||
                !scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                validation.Add(
                    "The Web Viewer product definition must reference one " +
                    "project-owned scene below Assets.");
            }

            Type requiredFeatureType =
                definition.RequiredDomainFeatureScript == null
                    ? null
                    : definition.RequiredDomainFeatureScript.GetClass();
            if (requiredFeatureType == null ||
                !typeof(ViewerFeatureBehaviour).IsAssignableFrom(
                    requiredFeatureType) ||
                requiredFeatureType.IsAbstract)
            {
                validation.Add(
                    "The product definition must reference one concrete " +
                    "ViewerFeatureBehaviour domain feature script.");
            }

            configuration = new WebViewerProductBuildConfiguration(
                definition,
                Normalize(scenePath),
                requiredFeatureType);
            return validation.IsValid;
        }

        internal static BuildProfile LoadProfile(
            DeucarianBuildEnvironment environment)
        {
            string path = WebViewerProductBuildWorkflow.GetProfilePath(
                environment);
            return AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
        }

        private static void ValidateIdentifier(
            string value,
            string label,
            DeucarianBuildValidationResult validation,
            int maximumLength = int.MaxValue)
        {
            ValidateText(value, label, validation);
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length > maximumLength)
            {
                validation.Add(
                    "The Web Viewer " + label + " may contain at most " +
                    maximumLength + " characters.");
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char character = trimmed[i];
                if (char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_' ||
                    character == '.')
                {
                    continue;
                }

                validation.Add(
                    "The Web Viewer " + label +
                    " may contain only letters, digits, '.', '-', and '_'.");
                return;
            }
        }

        private static void ValidateText(
            string value,
            string label,
            DeucarianBuildValidationResult validation)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                validation.Add("The Web Viewer " + label + " is required.");
            }
        }

        private static void ValidateOutputPath(
            string value,
            string label,
            DeucarianBuildValidationResult validation)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                validation.Add("The Web Viewer " + label + " is required.");
                return;
            }

            string normalized = Normalize(value.Trim());
            string[] segments = normalized.Split('/');
            bool invalidSegment = false;
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(segments[i]) ||
                    string.Equals(segments[i], ".", StringComparison.Ordinal) ||
                    string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    invalidSegment = true;
                    break;
                }
            }

            if (Path.IsPathRooted(normalized) ||
                segments.Length < 2 ||
                !string.Equals(
                    segments[0],
                    "Builds",
                    StringComparison.Ordinal) ||
                invalidSegment)
            {
                validation.Add(
                    "The Web Viewer " + label +
                    " must be a project-relative child of Builds.");
            }
        }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
    }
}
