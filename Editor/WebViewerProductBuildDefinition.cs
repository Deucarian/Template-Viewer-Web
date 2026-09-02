using UnityEditor;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Editor
{
    /// <summary>
    /// Project-owned data for one browser viewer product. Build behavior stays
    /// package-owned; products declare only identity, their real scene, and the
    /// one domain feature that must be present in that scene.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Deucarian/Viewer/Web Product Build Definition",
        fileName = "WebViewerProductBuildDefinition")]
    public sealed class WebViewerProductBuildDefinition : ScriptableObject
    {
        [SerializeField] private string providerId = "product-viewer";
        [SerializeField] private string displayName = "Product Viewer";
        [SerializeField] private SceneAsset viewerScene;
        [SerializeField] private string transportId = "product-viewer";
        [SerializeField] private string developmentBuildVersion =
            "local-product-viewer";
        [SerializeField] private string productionBuildVersion = "1.0";
        [SerializeField] private string developmentOutputPath =
            "Builds/WebViewer/Development";
        [SerializeField] private string productionOutputPath =
            "Builds/WebViewer/Production";
        [SerializeField] private MonoScript requiredDomainFeatureScript;

        public string ProviderId => providerId ?? string.Empty;

        public string DisplayName => displayName ?? string.Empty;

        public SceneAsset ViewerScene => viewerScene;

        public string TransportId => transportId ?? string.Empty;

        public string DevelopmentBuildVersion =>
            developmentBuildVersion ?? string.Empty;

        public string ProductionBuildVersion =>
            productionBuildVersion ?? string.Empty;

        public string DevelopmentOutputPath =>
            developmentOutputPath ?? string.Empty;

        public string ProductionOutputPath =>
            productionOutputPath ?? string.Empty;

        public MonoScript RequiredDomainFeatureScript =>
            requiredDomainFeatureScript;
    }
}
