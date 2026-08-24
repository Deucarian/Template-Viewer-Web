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
        [Tooltip("Additional exact HTTP(S) origins eligible for the live session provider. Unlisted absolute cross-origin URLs remain anonymous public downloads.")]
        [SerializeField] private List<string> authenticatedModelOrigins =
            new List<string>();
        private ViewerNavigationReferenceCompositionProfile _navigationComposition;
        private bool _hasResolvedNavigationComposition;
        private ViewerRenderingReferenceCompositionProfile _renderingComposition;
        private bool _hasResolvedRenderingComposition;

        private ObjectLoadingWebViewerModelLoader modelLoader;
        private WebViewerApplication application;
        private CommandRoutingRuntime<WebViewerApplication> commandRuntime;
        private CommandRoutePortBehaviour localCommandPort;
        private CommandTransportBridge<WebViewerApplication> commandBridge;
        private DiagnosticProviderRegistration diagnosticRegistration;
        private ViewerNavigationInstaller navigationInstaller;
        private ViewerRenderingInstaller renderingInstaller;
        private ViewerShellPresenter shellPresenter;
        private WebViewerShellStatusAdapter shellStatusAdapter;
        private DeucarianThemeProvider referenceThemeProvider;
        private DeucarianViewerReferenceThemeRuntime referenceThemeRuntime;
        private IViewerAuthenticationSession authenticationSession;
        private IViewerAuthenticationAcquisitionProvider
            authenticationAcquisitionProvider;
        private IDisposable authenticationTargetRegistration;
        private IDisposable runtimeConnection;
        private WebViewerFeatureBehaviour[] featureBehaviours =
            Array.Empty<WebViewerFeatureBehaviour>();

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
        public DeucarianViewerReferenceThemeRuntime ThemeRuntime =>
            referenceThemeRuntime;
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
        public CommandRoutePortBehaviour LocalCommandPort => localCommandPort;

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
            ReleaseComposition();
        }

        private void Compose()
        {
            try
            {
                ViewerRenderingInstaller rendering = InstallRendering();
                ViewerNavigationInstaller navigation = InstallNavigation();
                ViewerShellPresenter shell = InstallShell(rendering);

                if (!TryValidateConfiguration(false, out string issue))
                {
                    throw new InvalidOperationException(issue);
                }

                navigation.BeginReferenceLoad();

                ComposeAuthentication(
                    out IApiClient apiClient,
                    out string apiBaseUrl,
                    out IReadOnlyCollection<string>
                        effectiveAuthenticatedOrigins);
                modelLoader = new ObjectLoadingWebViewerModelLoader(
                    this,
                    apiClient,
                    loadedModelParent,
                    apiBaseUrl,
                    effectiveAuthenticatedOrigins);
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

                featureBehaviours = GetComponents<WebViewerFeatureBehaviour>();
                IWebViewerVisibilityFeatureFactory visibilityFactory =
                    ResolveVisibilityFeatureFactory(featureBehaviours);

                application = new WebViewerApplication(
                    new DirectWebViewerModelDescriptorResolver(),
                    modelLoader,
                    navigation,
                    eventPublisher,
                    embeddedReferenceModel,
                    authenticationSession,
                    visibilityFactory);
                var handlers = new List<ICommandHandler<WebViewerApplication>>(
                    WebViewerCommandHandlers.Create(
                        authenticationEventPublisher,
                        includeGenericVisibilityCommands:
                            visibilityFactory == null));
                for (int i = 0; i < featureBehaviours.Length; i++)
                {
                    WebViewerFeatureBehaviour feature = featureBehaviours[i];
                    feature.Attach(application);
                    IReadOnlyList<ICommandHandler<WebViewerApplication>>
                        featureHandlers = feature.CreateCommandHandlers();
                    if (featureHandlers == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < featureHandlers.Count; j++)
                    {
                        if (featureHandlers[j] == null)
                        {
                            throw new InvalidOperationException(
                                "A viewer feature returned a null command handler.");
                        }

                        handlers.Add(featureHandlers[j]);
                    }
                }

                commandRuntime = new CommandRoutingRuntime<WebViewerApplication>(
                    application,
                    handlers,
                    new CommandRoutingOptions(
                        historyCapacity: 64,
                        logSuccessfulCommands: false,
                        logFailedCommands: true));
                localCommandPort =
                    GetComponent<CommandRoutePortBehaviour>() ??
                    gameObject.AddComponent<CommandRoutePortBehaviour>();
                localCommandPort.Initialize(commandRuntime);
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
            catch
            {
                ReleaseComposition();
                throw;
            }
        }

        private void ComposeAuthentication(
            out IApiClient apiClient,
            out string apiBaseUrl,
            out IReadOnlyCollection<string> effectiveAuthenticatedOrigins)
        {
            ViewerRuntimeConnectionResolution resolution =
                ViewerRuntimeConnectionProviderRegistry.Resolve();
            if (resolution == null)
            {
                throw new InvalidOperationException(
                    "Runtime connection resolution returned no result.");
            }

            if (ShouldUseLocalAuthentication(resolution.Status))
            {
                var localSession = new ViewerAuthenticationSession();
                IApiClient localClient = ApiClientFactory.Create(
                    apiClientConfig,
                    localSession.ApiAuthProvider);
                IViewerAuthenticationAcquisitionProvider localProvider =
                    CreateAuthenticationAcquisitionProvider(localClient);
                IDisposable localRegistration =
                    ViewerAuthenticationTargetRegistry.Register(
                        "web-viewer-" + GetInstanceID(),
                        "Web Viewer",
                        localSession,
                        localProvider);

                authenticationSession = localSession;
                authenticationAcquisitionProvider = localProvider;
                authenticationTargetRegistration = localRegistration;
                apiClient = localClient;
                apiBaseUrl = apiClientConfig != null
                    ? apiClientConfig.BaseUrl
                    : null;
                effectiveAuthenticatedOrigins = MergeAuthenticatedOrigins(null);
                return;
            }

            ViewerRuntimeConnection connection = resolution.Connection;
            if (!IsValidRuntimeConnection(connection))
            {
                connection?.Dispose();
                throw new InvalidOperationException(
                    "The resolved runtime connection is incomplete.");
            }

            try
            {
                ViewerAuthenticationTargetRegistry.TryGet(
                    connection.TargetId,
                    out ViewerAuthenticationTarget target);
                authenticationSession = connection.Session;
                authenticationAcquisitionProvider =
                    target?.AcquisitionProvider;
                authenticationTargetRegistration = null;
                apiClient = connection.ApiClient;
                apiBaseUrl = connection.ApiBaseUrl;
                effectiveAuthenticatedOrigins = MergeAuthenticatedOrigins(
                    connection.AuthenticatedOrigins);
                runtimeConnection = connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static bool ShouldUseLocalAuthentication(
            ViewerRuntimeConnectionResolutionStatus status)
        {
            switch (status)
            {
                case ViewerRuntimeConnectionResolutionStatus.None:
                    return true;
                case ViewerRuntimeConnectionResolutionStatus.Resolved:
                    return false;
                case ViewerRuntimeConnectionResolutionStatus.Failed:
                    throw new InvalidOperationException(
                        "The optional runtime connection provider failed.");
                case ViewerRuntimeConnectionResolutionStatus.Ambiguous:
                    throw new InvalidOperationException(
                        "Multiple runtime connection providers are active.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "Unknown runtime connection resolution status.");
            }
        }

        private static bool IsValidRuntimeConnection(
            ViewerRuntimeConnection connection)
        {
            if (connection == null ||
                string.IsNullOrWhiteSpace(connection.TargetId) ||
                connection.Session == null ||
                connection.ApiClient == null)
            {
                return false;
            }

            if (!ViewerAuthenticationTargetRegistry.TryGet(
                    connection.TargetId,
                    out ViewerAuthenticationTarget target) ||
                !ReferenceEquals(target.Session, connection.Session))
            {
                return false;
            }

            return Uri.TryCreate(
                       connection.ApiBaseUrl,
                       UriKind.Absolute,
                       out Uri baseUri) &&
                   (baseUri.Scheme == Uri.UriSchemeHttp ||
                    baseUri.Scheme == Uri.UriSchemeHttps) &&
                   string.IsNullOrEmpty(baseUri.UserInfo) &&
                   string.IsNullOrEmpty(baseUri.Query) &&
                   string.IsNullOrEmpty(baseUri.Fragment);
        }

        private IReadOnlyCollection<string> MergeAuthenticatedOrigins(
            IEnumerable<string> connectionOrigins)
        {
            var merged = new List<string>();
            AddOrigins(merged, connectionOrigins);
            AddOrigins(merged, authenticatedModelOrigins);
            return merged;
        }

        private static void AddOrigins(
            ICollection<string> destination,
            IEnumerable<string> origins)
        {
            if (origins == null)
            {
                return;
            }

            foreach (string origin in origins)
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    continue;
                }

                string normalized = origin.Trim();
                bool exists = false;
                foreach (string current in destination)
                {
                    if (string.Equals(
                            current,
                            normalized,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    destination.Add(normalized);
                }
            }
        }

        private void ReleaseComposition()
        {
            WebViewerShellStatusAdapter statusAdapter = shellStatusAdapter;
            shellStatusAdapter = null;
            TryCleanup(() => statusAdapter?.Dispose());

            CommandTransportBridge<WebViewerApplication> bridge = commandBridge;
            commandBridge = null;
            TryCleanup(() => bridge?.Dispose());

            CommandRoutePortBehaviour routePort = localCommandPort;
            CommandRoutingRuntime<WebViewerApplication> runtime = commandRuntime;
            localCommandPort = null;
            TryCleanup(() => routePort?.Clear(runtime));

            WebViewerApplication currentApplication = application;
            application = null;
            WebViewerFeatureBehaviour[] features = featureBehaviours;
            featureBehaviours = Array.Empty<WebViewerFeatureBehaviour>();
            for (int i = features.Length - 1; i >= 0; i--)
            {
                WebViewerFeatureBehaviour feature = features[i];
                TryCleanup(() => feature?.Detach(currentApplication));
            }

            TryCleanup(() => currentApplication?.Dispose());

            commandRuntime = null;
            TryCleanup(() => runtime?.Dispose());

            DiagnosticProviderRegistration diagnostics = diagnosticRegistration;
            diagnosticRegistration = null;
            TryCleanup(() => diagnostics?.Dispose());

            ViewerShellPresenter presenter = shellPresenter;
            shellPresenter = null;
            TryCleanup(() => presenter?.Dispose());

            navigationInstaller = null;
            renderingInstaller = null;

            ObjectLoadingWebViewerModelLoader loader = modelLoader;
            modelLoader = null;
            if (loader != null)
            {
                loader.ProgressChanged -= OnModelLoadingProgress;
                TryCleanup(loader.Dispose);
            }

            ReleaseAuthenticationComposition();
        }

        private static IWebViewerVisibilityFeatureFactory
            ResolveVisibilityFeatureFactory(
                IReadOnlyList<WebViewerFeatureBehaviour> features)
        {
            IWebViewerVisibilityFeatureFactory result = null;
            for (int i = 0; i < features.Count; i++)
            {
                IWebViewerVisibilityFeatureFactory candidate =
                    features[i].VisibilityFeatureFactory;
                if (candidate == null)
                {
                    continue;
                }

                if (result != null && !ReferenceEquals(result, candidate))
                {
                    throw new InvalidOperationException(
                        "Only one viewer feature may own model visibility.");
                }

                result = candidate;
            }

            return result;
        }

        private void ReleaseAuthenticationComposition()
        {
            IDisposable targetRegistration = authenticationTargetRegistration;
            authenticationTargetRegistration = null;
            TryCleanup(() => targetRegistration?.Dispose());

            IDisposable connection = runtimeConnection;
            runtimeConnection = null;
            TryCleanup(() => connection?.Dispose());

            authenticationAcquisitionProvider = null;
            authenticationSession = null;
        }

        private static void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup?.Invoke();
            }
            catch (Exception)
            {
                // Cleanup is best-effort and must continue so no later
                // transport, route, target, or session lease remains live.
            }
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
            referenceThemeRuntime =
                DeucarianViewerReferenceThemeComposition.Install(
                    gameObject,
                    referenceThemeProvider);
            referenceThemeProvider = referenceThemeRuntime.Provider;
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
