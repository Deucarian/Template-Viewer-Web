using Deucarian.SimultriaViewerIntegration;
using Deucarian.WebGLTemplate;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration
{
    internal readonly struct WebViewerStartupStatusProjection
    {
        private WebViewerStartupStatusProjection(string phase, string value, bool failed = false)
        {
            Phase = phase;
            Value = value;
            Failed = failed;
        }

        internal string Phase { get; }
        internal string Value { get; }
        internal bool Failed { get; }

        internal static WebViewerStartupStatusProjection From(
            SimultriaViewerBuildStartupSnapshot snapshot)
        {
            if (snapshot == null) return default;
            switch (snapshot.Phase)
            {
                case SimultriaViewerBuildStartupPhase.Resolving:
                    return new WebViewerStartupStatusProjection("resolving_environment", string.Empty);
                case SimultriaViewerBuildStartupPhase.Failed:
                    return new WebViewerStartupStatusProjection(null,
                        snapshot.FailureCode == SimultriaViewerBuildStartupFailureCode.EnvironmentResolutionFailed
                            ? "viewer_environment_resolution_failed" : "viewer_connection_failed", true);
                case SimultriaViewerBuildStartupPhase.Fallback:
                    // The immutable gate already bounds this value. Keep this
                    // adapter allowlist explicit too; never forward arbitrary IDs.
                    switch (snapshot.EnvironmentId.Value)
                    {
                        case "simultria.production":
                            return new WebViewerStartupStatusProjection("build_profile_fallback", "production");
                        case "simultria.development":
                            return new WebViewerStartupStatusProjection("build_profile_fallback", "development");
                        case "simultria.testing":
                            return new WebViewerStartupStatusProjection("build_profile_fallback", "testing");
                        case "simultria.acceptance":
                            return new WebViewerStartupStatusProjection("build_profile_fallback", "acceptance");
                        default:
                            return new WebViewerStartupStatusProjection(null,
                                "viewer_environment_resolution_failed", true);
                    }
                default:
                    // Routed means connection resolution, not model/engine ready.
                    return default;
            }
        }

        internal static void Publish(WebViewerStartupStatusProjection projection)
        {
            if (projection.Failed)
            {
                DeucarianWebGLShell.ReportState(
                    DeucarianWebGLShellState.Failed, projection.Value);
            }
            else if (!string.IsNullOrEmpty(projection.Phase))
            {
                DeucarianWebGLShell.ReportProgress(projection.Phase, 0f, projection.Value);
            }
        }
    }
}
