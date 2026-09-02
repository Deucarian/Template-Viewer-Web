using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Deucarian.BuildPipeline;
using Deucarian.Theming;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Editor
{
    /// <summary>
    /// Validates the shared in-player presentation and writes the same theme
    /// snapshot for every declarative Web Viewer product's browser first paint.
    /// </summary>
    public sealed class WebViewerThemeBuildProcessor :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        internal const string GeneratedThemeRelativePath =
            "TemplateData/theme.generated.js";

        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ProcessPrebuild(
                report.summary.platform,
                WebViewerProductBuildExecutionScope.Current,
                Validate);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            ProcessPostbuild(
                report.summary.platform,
                WebViewerProductBuildExecutionScope.Current,
                report.summary.outputPath,
                path => WriteGeneratedThemeFile(path));
        }

        internal static void ProcessPrebuild(
            BuildTarget platform,
            WebViewerProductBuildConfiguration configuration,
            Func<DeucarianBuildValidationResult> validate)
        {
            if (!ShouldProcess(platform, configuration))
            {
                return;
            }

            if (validate == null)
            {
                throw new ArgumentNullException(nameof(validate));
            }

            DeucarianBuildValidationResult validation = validate();
            if (validation == null)
            {
                throw new BuildFailedException(
                    "Shared Web Viewer presentation validation returned " +
                    "no result.");
            }

            if (!validation.IsValid)
            {
                throw new BuildFailedException(validation.Format(
                    "Shared Web Viewer presentation"));
            }
        }

        internal static void ProcessPostbuild(
            BuildTarget platform,
            WebViewerProductBuildConfiguration configuration,
            string outputPath,
            Action<string> write)
        {
            if (!ShouldProcess(platform, configuration))
            {
                return;
            }

            if (write == null)
            {
                throw new ArgumentNullException(nameof(write));
            }

            write(outputPath);
        }

        internal static bool ShouldProcess(
            BuildTarget platform,
            WebViewerProductBuildConfiguration configuration) =>
            platform == BuildTarget.WebGL && configuration != null;

        internal static DeucarianBuildValidationResult Validate()
        {
            var result = new DeucarianBuildValidationResult();
            try
            {
                LoadAndValidateDefaultTheme();
            }
            catch (Exception exception)
            {
                result.Add(exception.GetBaseException().Message);
            }

            ValidateTextMeshProResources(result);

            return result;
        }

        internal static string WriteGeneratedThemeFile(
            string webGlOutputPath)
        {
            if (string.IsNullOrWhiteSpace(webGlOutputPath))
            {
                throw new BuildFailedException(
                    "The WebGL theme snapshot cannot be written because " +
                    "the build output path is empty.");
            }

            string outputDirectory = ResolveOutputDirectory(webGlOutputPath);
            string generatedThemePath = Path.Combine(
                outputDirectory,
                GeneratedThemeRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(generatedThemePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new BuildFailedException(
                    "The WebGL theme snapshot output directory could not " +
                    "be resolved.");
            }

            Directory.CreateDirectory(directory);
            string script = CreateGeneratedThemeScript(
                LoadAndValidateDefaultTheme());
            File.WriteAllText(
                generatedThemePath,
                script,
                new UTF8Encoding(false));
            return generatedThemePath;
        }

        internal static string CreateGeneratedThemeScript(
            DeucarianTheme theme)
        {
            ValidateTheme(theme, "default");
            DeucarianViewerThemeSnapshot snapshot =
                DeucarianViewerThemeSnapshot.FromTheme(
                    theme,
                    theme.VisualStyle);
            if (!snapshot.IsValid)
            {
                throw new BuildFailedException(
                    "The WebGL theme snapshot is incomplete. Missing roles: " +
                    string.Join(
                        ", ",
                        snapshot.MissingRoles ?? Array.Empty<string>()) +
                    ".");
            }

            string json = snapshot.ToJson().Replace("</", "<\\/");
            return "window.deucarianWebGLInitialTheme = " + json + ";\n";
        }

        internal static DeucarianTheme LoadAndValidateDefaultTheme()
        {
            DeucarianViewerReferenceThemeProfile profile =
                DeucarianViewerReferenceThemePreset.Resolve();
            if (profile == null ||
                profile.ThemeFamily == null ||
                !profile.ThemeFamily.IsComplete)
            {
                throw new BuildFailedException(
                    "WebGL builds require the complete shared viewer " +
                    "reference theme family.");
            }

            if (!string.Equals(
                    profile.ThemeFamily.FamilyId,
                    DeucarianViewerReferenceThemePreset.FamilyId,
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "The shared viewer reference theme family has an " +
                    "unexpected family ID.");
            }

            ValidateThemeVariant(
                profile,
                profile.LightTheme,
                DeucarianThemeMode.Light);
            ValidateThemeVariant(
                profile,
                profile.DarkTheme,
                DeucarianThemeMode.Dark);
            DeucarianTheme theme = profile.ResolveTheme(
                DeucarianViewerReferenceThemePreset.DefaultMode);
            ValidateTheme(theme, "reference default");
            return theme;
        }

        private static void ValidateThemeVariant(
            DeucarianViewerReferenceThemeProfile profile,
            DeucarianTheme theme,
            DeucarianThemeMode mode)
        {
            if (theme != profile.ThemeFamily.GetTheme(mode))
            {
                throw new BuildFailedException(
                    "The shared viewer reference theme family does not " +
                    "resolve its " + mode + " variant consistently.");
            }

            ValidateTheme(theme, mode + " reference");
            DeucarianColorPalette palette = theme.ColorPalette;
            if (!palette.HasThemeMode || palette.ThemeMode != mode)
            {
                throw new BuildFailedException(
                    "The shared viewer " + mode + " palette must be " +
                    "explicitly marked as " + mode + ".");
            }
        }

        private static void ValidateTheme(
            DeucarianTheme theme,
            string variantName)
        {
            if (theme == null || theme.ColorPalette == null)
            {
                throw new BuildFailedException(
                    "The " + variantName +
                    " WebGL theme and color palette are required.");
            }

            if (string.IsNullOrWhiteSpace(theme.ThemeId) ||
                string.IsNullOrWhiteSpace(theme.ColorPalette.PaletteId))
            {
                throw new BuildFailedException(
                    "The " + variantName +
                    " WebGL theme and palette require stable IDs.");
            }

            DeucarianViewerThemeSnapshot snapshot =
                DeucarianViewerThemeSnapshot.FromTheme(
                    theme,
                    theme.VisualStyle);
            if (!snapshot.IsValid)
            {
                throw new BuildFailedException(
                    "The " + variantName +
                    " WebGL theme is missing required semantic roles: " +
                    string.Join(
                        ", ",
                        snapshot.MissingRoles ?? Array.Empty<string>()) +
                    ".");
            }

            List<string> issues =
                theme.ColorPalette.GetValidationWarnings();
            if (issues.Count > 0)
            {
                throw new BuildFailedException(
                    "The " + variantName +
                    " WebGL theme failed validation:\n- " +
                    string.Join("\n- ", issues));
            }
        }

        private static void ValidateTextMeshProResources(
            DeucarianBuildValidationResult result)
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>(
                "TMP Settings");
            if (settings == null)
            {
                result.Add(
                    "WebGL build requires TextMesh Pro Essential Resources. " +
                    "Import them before building.");
                return;
            }

            try
            {
                if (TMP_Settings.fontFeatures == null ||
                    TMP_Settings.defaultFontAsset == null)
                {
                    result.Add(
                        "TMP Settings must contain its active font features " +
                        "and default font asset.");
                }
            }
            catch (Exception)
            {
                result.Add(
                    "TMP Settings could not initialize its active font " +
                    "features or default font.");
            }
        }

        private static string ResolveOutputDirectory(string outputPath)
        {
            string fullPath = Path.GetFullPath(outputPath);
            if (!File.Exists(fullPath) &&
                !string.Equals(
                    Path.GetExtension(fullPath),
                    ".html",
                    StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new BuildFailedException(
                    "The WebGL build output directory could not be resolved.");
            }

            return directory;
        }
    }

    internal sealed class WebViewerProductBuildExecutionScope : IDisposable
    {
        [ThreadStatic]
        private static WebViewerProductBuildConfiguration current;

        private readonly WebViewerProductBuildConfiguration previous;
        private bool disposed;

        private WebViewerProductBuildExecutionScope(
            WebViewerProductBuildConfiguration configuration)
        {
            previous = current;
            current = configuration ??
                throw new ArgumentNullException(nameof(configuration));
        }

        internal static WebViewerProductBuildConfiguration Current => current;

        internal static WebViewerProductBuildExecutionScope Enter(
            WebViewerProductBuildConfiguration configuration) =>
            new WebViewerProductBuildExecutionScope(configuration);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current = previous;
        }
    }
}
