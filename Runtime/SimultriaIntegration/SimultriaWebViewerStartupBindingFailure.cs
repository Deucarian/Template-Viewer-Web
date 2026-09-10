namespace Deucarian.TemplateViewerWeb.SimultriaIntegration
{
    /// <summary>
    /// Local, bounded bridge-binding diagnostics. These never contain object
    /// names, scene paths, endpoints, exception text or application payloads.
    /// They describe this observer's wiring, not the connection gate's outcome.
    /// </summary>
    public enum SimultriaWebViewerStartupBindingFailure
    {
        None = 0,
        ViewerMissing = 1,
        ConnectionGateMissing = 2,
        InvalidScene = 3,
        SceneMismatch = 4,
        ViewerNotOwned = 5
    }
}
