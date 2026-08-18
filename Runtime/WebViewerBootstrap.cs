using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.Diagnostics;
using Deucarian.Logging;
using Deucarian.Session.APIIntegration;
using Deucarian.TemplateViewerWeb.Commands;
using Deucarian.TemplateViewerWeb.Diagnostics;
using Deucarian.TemplateViewerWeb.Loading;
using Deucarian.TemplateViewerWeb.Selection;
using Deucarian.Theming;
using Deucarian.ViewerAuthentication;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerNavigation.UI;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    [DisallowMultipleComponent]
    public sealed class WebViewerBootstrap : MonoBehaviour
    {
        private static readonly DLog Log = DLog.For("TemplateViewerWeb");

        [Header("Browser transport")]
        [SerializeField] private bool iframeMode;
        [SerializeField] private string parentOrigin = "http://localhost:8080";
        [SerializeField] private string transportId = "web-viewer";

        [Header("Viewer")]
        [SerializeField] private Camera viewerCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private ViewerNavigationSettings navigationSettings;
        [SerializeField] private GameObject embeddedReferenceModel;
        [SerializeField] private Transform loadedModelParent;
        [SerializeField] private ApiClientConfig apiClientConfig;

        [Header("Authentication")]
        [Tooltip("Optional credential-free token endpoint profile. When omitted, the shared Viewer Authentication Resources profile is used when present.")]
        [SerializeField] private SessionTokenEndpointProfile
            authenticationTokenEndpointProfile;
        [Tooltip("Additional exact HTTP(S) origins that may receive the live viewer session token for model downloads. Cross-origin URLs stay anonymous unless listed here.")]
        [SerializeField] private List<string> authenticatedModelOrigins =
            new List<string>();
        private ViewerNavigationReferenceCompositionProfile _navigationComposition;
        private bool _hasResolvedNavigationComposition;
        private ViewerRenderingReferenceCompositionProfile _renderingComposition;
        private bool _hasResolvedRenderingComposition;

        private ObjectLoadingWebViewerModelLoader modelLoader;
        private WebViewerApplication application;
        private CommandRoutingRuntime<WebViewerApplication> commandRuntime;
        private CommandTransportBridge<WebViewerApplication> commandBridge;
        private DiagnosticProviderRegistration diagnosticRegistration;
        private ViewerNavigationInstaller navigationInstaller;
        private ViewerRenderingInstaller renderingInstaller;
        private ViewerShellPresenter shellPresenter;
        private WebViewerShellStatusAdapter shellStatusAdapter;
        private DeucarianThemeProvider referenceThemeProvider;
        private ViewerAuthenticationSession authenticationSession;
        private IViewerAuthenticationAcquisitionProvider
            authenticationAcquisitionProvider;
        private IDisposable authenticationTargetRegistration;

        public bool IframeMode => iframeMode;
        public string ParentOrigin => parentOrigin;
        public WebViewerApplication Application => application;
        public ViewerNavigationReferenceCompositionProfile
            ResolvedNavigationComposition => ResolveNavigationComposition();
        public ViewerNavigationSettings ResolvedNavigationSettings =>
            ResolvedNavigationComposition.Preset;
        public ViewerRenderingReferenceCompositionProfile
            ResolvedRenderingComposition => ResolveRenderingComposition();
        public ViewerShellReferenceProfile ResolvedShellProfile =>
            ViewerShellReferencePreset.Profile;
        public ViewerNavigationInstaller NavigationInstaller =>
            navigationInstaller;
        public ViewerRenderingInstaller RenderingInstaller =>
            renderingInstaller;
        public ViewerShellPresenter ShellPresenter => shellPresenter;
        public DeucarianThemeProvider ThemeProvider =>
            referenceThemeProvider ??
            renderingInstaller?.ThemeProvider ??
            navigationInstaller?.ThemeProvider ??
            shellPresenter?.ThemeProvider;
        public DeucarianTheme CurrentTheme =>
            ThemeProvider?.CurrentTheme ??
            ResolvedNavigationComposition.ThemeProfile.ResolveTheme(
                ResolvedNavigationComposition.ThemeMode);
        public SessionTokenEndpointProfile
            ResolvedAuthenticationTokenEndpointProfile =>
                authenticationTokenEndpointProfile ??
                Resources.Load<SessionTokenEndpointProfile>(
                    ViewerAuthenticationEndpointProviderFactory
                        .DefaultProfileResourcePath);
        public IViewerAuthenticationAcquisitionProvider
            AuthenticationAcquisitionProvider =>
                authenticationAcquisitionProvider;

        private void Start()
        {
            try
            {
                Compose();
            }
            catch (Exception exception)
            {
                Log.Error(
                    "Web viewer composition failed with " +
                    exception.GetType().Name + ". Details were omitted.",
                    this);
                shellPresenter?.ApplyStatus(
                    ViewerShellStatusSnapshot.Error(
                        "Viewer configuration failed",
                        "The generic viewer composition did not complete."));
            }
        }

        public bool TryValidateConfiguration(
            bool production,
            out string issue)
        {
            if (!iframeMode)
            {
                issue = string.Empty;
                return true;
            }

            if (!Uri.TryCreate(parentOrigin, UriKind.Absolute, out Uri origin) ||
                (origin.Scheme != Uri.UriSchemeHttp &&
                 origin.Scheme != Uri.UriSchemeHttps) ||
                origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) ||
                !string.IsNullOrEmpty(origin.Fragment) ||
                !string.IsNullOrEmpty(origin.UserInfo))
            {
                issue = "Iframe mode requires an exact HTTP(S) parent origin.";
                return false;
            }

            if (production &&
                (origin.Scheme != Uri.UriSchemeHttps ||
                 origin.IsLoopback))
            {
                issue = "Production iframe mode requires an exact non-loopback HTTPS origin.";
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            authenticationTargetRegistration?.Dispose();
            authenticationTargetRegistration = null;
            authenticationAcquisitionProvider = null;
            shellStatusAdapter?.Dispose();
            shellStatusAdapter = null;
            commandBridge?.Dispose();
            commandBridge = null;
            application?.Dispose();
            application = null;
            commandRuntime?.Dispose();
            commandRuntime = null;
            diagnosticRegistration?.Dispose();
            diagnosticRegistration = null;
            shellPresenter?.Dispose();
            shellPresenter = null;
            navigationInstaller = null;
            renderingInstaller = null;
            if (modelLoader != null)
            {
                modelLoader.ProgressChanged -= OnModelLoadingProgress;
                modelLoader = null;
            }
        }

        private void Compose()
        {
            ViewerRenderingInstaller rendering = InstallRendering();
            ViewerNavigationInstaller navigation = InstallNavigation();
            ViewerShellPresenter shell = InstallShell(rendering);

            if (!TryValidateConfiguration(false, out string issue))
            {
                throw new InvalidOperationException(issue);
            }

            navigation.BeginReferenceLoad();

            authenticationSession = new ViewerAuthenticationSession();
            IApiClient apiClient = ApiClientFactory.Create(
                apiClientConfig,
                authenticationSession.ApiAuthProvider);
            authenticationAcquisitionProvider =
                CreateAuthenticationAcquisitionProvider(apiClient);
            authenticationTargetRegistration =
                ViewerAuthenticationTargetRegistry.Register(
                    "web-viewer-" + GetInstanceID(),
                    "Web Viewer",
                    authenticationSession,
                    authenticationAcquisitionProvider);
            modelLoader = new ObjectLoadingWebViewerModelLoader(
                this,
                apiClient,
                loadedModelParent,
                apiClientConfig != null ? apiClientConfig.BaseUrl : null,
                authenticatedModelOrigins);
            modelLoader.ProgressChanged += OnModelLoadingProgress;

            WebGlCommandTransportMode mode = iframeMode
                ? WebGlCommandTransportMode.ParentIframe
                : WebGlCommandTransportMode.DirectPage;
            string[] allowedOrigins = iframeMode
                ? new[] { parentOrigin }
                : Array.Empty<string>();
            var transportOptions = new WebGlCommandTransportOptions(
                transportId,
                mode,
                allowedOrigins,
                iframeMode ? parentOrigin : null);
            var transport = new WebGlCommandTransport(transportOptions);
            WebGlCommandTransportBehaviour behaviour =
                gameObject.AddComponent<WebGlCommandTransportBehaviour>();
            behaviour.Initialize(transport);

            string browserEndpoint = iframeMode
                ? "parent:" + transportOptions.TargetOrigin
                : "direct";
            var eventPublisher =
                new WebGlWebViewerEventPublisher(transport);
            var authenticationEventPublisher =
                new WebViewerAuthenticationEventPublisher(
                    eventPublisher,
                    browserEndpoint);

            application = new WebViewerApplication(
                new DirectWebViewerModelDescriptorResolver(),
                modelLoader,
                navigation,
                eventPublisher,
                embeddedReferenceModel,
                authenticationSession);
            commandRuntime = new CommandRoutingRuntime<WebViewerApplication>(
                application,
                WebViewerCommandHandlers.Create(authenticationEventPublisher),
                new CommandRoutingOptions(
                    historyCapacity: 64,
                    logSuccessfulCommands: false,
                    logFailedCommands: true));
            commandBridge = new CommandTransportBridge<WebViewerApplication>(
                commandRuntime,
                transport,
                shouldSendResponses: true,
                disposeTransport: true);
            diagnosticRegistration = DiagnosticProviderRegistry.Register(
                new WebViewerApplicationDiagnosticProvider(application));
            shellStatusAdapter = new WebViewerShellStatusAdapter(
                application,
                shell);
            commandBridge.Start();
        }

        private IViewerAuthenticationAcquisitionProvider
            CreateAuthenticationAcquisitionProvider(IApiClient apiClient)
        {
            SessionTokenEndpointProfile profile =
                ResolvedAuthenticationTokenEndpointProfile;
            return profile == null
                ? null
                : ViewerAuthenticationEndpointProviderFactory.Create(
                    profile,
                    apiClient);
        }

        private ViewerRenderingInstaller InstallRendering()
        {
            EnsureSceneDependencies();
            renderingInstaller = ResolvedRenderingComposition.Compose(
                transform,
                viewerCamera,
                keyLight,
                referenceThemeProvider);
            viewerCamera = renderingInstaller.Camera;
            keyLight = renderingInstaller.KeyLight;
            referenceThemeProvider = renderingInstaller.ThemeProvider;
            return renderingInstaller;
        }

        private ViewerNavigationInstaller InstallNavigation()
        {
            ViewerRenderingInstaller rendering =
                renderingInstaller ?? InstallRendering();
            navigationInstaller = ResolvedNavigationComposition.Compose(
                transform,
                viewerCamera,
                rendering.ThemeProvider);
            return navigationInstaller;
        }

        private ViewerShellPresenter InstallShell(
            ViewerRenderingInstaller rendering)
        {
            ViewerShellConfiguration configuration =
                ViewerShellReferenceComposition.CreateConfiguration(
                    rendering.ThemeProvider,
                    () => ViewerNavigationMotionPreferences.ShouldAnimate,
                    root => ViewerNavigationMovementKeyGuard.Bind(root),
                    showDiagnostics: true);
            shellPresenter = ViewerShellReferenceComposition.Install(
                transform,
                rendering.Controller,
                configuration);
            return shellPresenter;
        }

        private ViewerNavigationReferenceCompositionProfile
            ResolveNavigationComposition()
        {
            if (_hasResolvedNavigationComposition)
            {
                return _navigationComposition;
            }

            ViewerNavigationReferenceCompositionProfile referenceComposition =
                ViewerNavigationReferenceComposition.Resolve();
            _navigationComposition = navigationSettings == null
                ? referenceComposition
                : referenceComposition.WithPreset(navigationSettings);
            _hasResolvedNavigationComposition = true;
            return _navigationComposition;
        }

        private ViewerRenderingReferenceCompositionProfile
            ResolveRenderingComposition()
        {
            if (!_hasResolvedRenderingComposition)
            {
                _renderingComposition =
                    ViewerRenderingReferenceComposition.Resolve();
                _hasResolvedRenderingComposition = true;
            }

            return _renderingComposition;
        }

        private void EnsureSceneDependencies()
        {
            if (loadedModelParent == null)
            {
                GameObject parent = new GameObject("Loaded Model");
                parent.transform.SetParent(transform, false);
                loadedModelParent = parent.transform;
            }

            if (embeddedReferenceModel == null)
            {
                embeddedReferenceModel = CreateEmbeddedReferenceModel();
            }
        }

        private GameObject CreateEmbeddedReferenceModel()
        {
            GameObject root = new GameObject("Embedded Reference Model");
            root.transform.SetParent(transform, false);
            CreateElement(root.transform, "red", PrimitiveType.Cube, new Vector3(-2.2f, 0f, 0f));
            CreateElement(root.transform, "green", PrimitiveType.Sphere, Vector3.zero);
            CreateElement(root.transform, "blue", PrimitiveType.Capsule, new Vector3(2.2f, 0f, 0f));
            return root;
        }

        private static void CreateElement(
            Transform parent,
            string id,
            PrimitiveType primitiveType,
            Vector3 position)
        {
            GameObject element = GameObject.CreatePrimitive(primitiveType);
            element.name = "Element " + id;
            element.transform.SetParent(parent, false);
            element.transform.localPosition = position;
            element.AddComponent<WebViewerElement>().Initialize(id);
        }

        private void OnModelLoadingProgress(float normalized, string message)
        {
            application?.ReportLoadingProgress(normalized, message);
        }
    }
}
