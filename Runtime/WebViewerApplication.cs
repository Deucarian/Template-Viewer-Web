using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewerWeb.Selection;
using Deucarian.ViewerAuthentication;
using Deucarian.ViewerNavigation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    public sealed class WebViewerApplication :
        IDisposable,
        IViewerAuthenticationHost
    {
        private readonly IWebViewerModelDescriptorResolver descriptorResolver;
        private readonly IWebViewerModelLoader modelLoader;
        private readonly ViewerNavigationInstaller navigation;
        private readonly IWebViewerEventPublisher eventPublisher;
        private readonly GameObject embeddedModel;
        private readonly IViewerAuthenticationSession authenticationSession;
        private readonly IWebViewerVisibilityFeatureFactory
            visibilityFeatureFactory;
        private CancellationTokenSource initializationCancellation;
        private IWebViewerVisibilityFeature visibilityFeature;
        private WebViewerSelectionStateOwner selection;
        private int initializationGeneration;
        private long latestRevision = -1;
        private bool disposed;

        public WebViewerApplication(
            IWebViewerModelDescriptorResolver resolver,
            IWebViewerModelLoader loader,
            ViewerNavigationInstaller navigationInstaller,
            IWebViewerEventPublisher publisher,
            GameObject embeddedReferenceModel = null,
            IViewerAuthenticationSession viewerAuthentication = null,
            IWebViewerVisibilityFeatureFactory customVisibilityFeatureFactory = null)
        {
            descriptorResolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));
            modelLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            navigation = navigationInstaller ??
                throw new ArgumentNullException(nameof(navigationInstaller));
            eventPublisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            embeddedModel = embeddedReferenceModel;
            authenticationSession = viewerAuthentication ??
                new ViewerAuthenticationSession();
            visibilityFeatureFactory = customVisibilityFeatureFactory;
            Lifecycle = WebViewerLifecycleState.Created;
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }
        }

        public event Action<WebViewerLifecycleState> LifecycleChanged;
        public event Action<float, string> LoadingProgressChanged;
        public event Action<WebViewerModelContext> ModelReady;
        public event Action<WebViewerModelContext> ModelUnloading;

        public WebViewerLifecycleState Lifecycle { get; private set; }
        public long LatestRevision => Interlocked.Read(ref latestRevision);
        public int IndexedElementCount =>
            visibilityFeature?.IndexedElementCount ?? 0;
        public int SelectedElementCount =>
            visibilityFeature?.SelectedElementCount ?? 0;
        public WebViewerModelContext CurrentModel { get; private set; }
        public IViewerAuthenticationSession AuthenticationSession =>
            authenticationSession;

        public async Task<CommandOperationResult> InitializeAsync(
            WebViewerInitializeRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (disposed)
            {
                return CommandOperationResult.Failure(
                    "viewer_disposed",
                    "The viewer application is disposed.");
            }

            if (!descriptorResolver.TryResolve(
                    request,
                    out WebViewerModelDescriptor descriptor,
                    out string validationError))
            {
                return CommandOperationResult.Failure(
                    "invalid_initialization",
                    validationError);
            }

            if (!TryAdvanceRevision(request.Revision))
            {
                return CommandOperationResult.Failure(
                    "stale_revision",
                    "The initialization revision is stale.");
            }

            int generation = Interlocked.Increment(ref initializationGeneration);
            CancelInitialization();
            initializationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = initializationCancellation.Token;
            try
            {
                ResetCurrentModel();
                SetLifecycle(WebViewerLifecycleState.Loading);
                await eventPublisher.PublishAsync(
                    "viewer_loading",
                    new JObject { ["revision"] = request.Revision },
                    remoteEndpoint,
                    token);
                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                GameObject referenceRoot;
                if (descriptor.UsesEmbeddedModel)
                {
                    referenceRoot = embeddedModel;
                    if (referenceRoot != null)
                    {
                        referenceRoot.SetActive(true);
                    }
                }
                else
                {
                    WebViewerModelLoadResult loadResult =
                        await modelLoader.LoadAsync(descriptor, token);
                    if (!IsInitializationCurrent(generation, token))
                    {
                        return SupersededInitialization();
                    }

                    if (loadResult == null || !loadResult.Succeeded)
                    {
                        return await FailInitializationAsync(
                            request.Revision,
                            loadResult?.Message ?? "The model did not load.",
                            remoteEndpoint,
                            generation,
                            token);
                    }

                    referenceRoot = loadResult.ReferenceRoot;
                }

                if (referenceRoot == null)
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        "No embedded model or model_url was supplied.",
                        remoteEndpoint,
                        generation,
                        token);
                }

                var modelContext = new WebViewerModelContext(
                    referenceRoot,
                    descriptor,
                    request.Revision);
                if (!TryCreateVisibilityFeature(
                        modelContext,
                        out IWebViewerVisibilityFeature createdFeature,
                        out string featureError))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        featureError,
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                visibilityFeature = createdFeature;
                selection = (createdFeature as GenericWebViewerVisibilityFeature)
                    ?.Selection;
                if (!navigation.RegisterReference(referenceRoot, true, true))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        "The model contains no renderable reference bounds.",
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                SetLifecycle(WebViewerLifecycleState.Ready);
                CurrentModel = modelContext;
                NotifyModelReady(modelContext);
                await eventPublisher.PublishAsync(
                    "viewer_ready",
                    new JObject
                    {
                        ["revision"] = request.Revision,
                        ["model_id"] = descriptor.ModelId,
                        ["model_version"] = descriptor.ModelVersion,
                        ["element_count"] = IndexedElementCount
                    },
                    remoteEndpoint,
                    token);
                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                return CommandOperationResult.Success(new JObject
                {
                    ["revision"] = request.Revision,
                    ["element_count"] = IndexedElementCount
                });
            }
            catch (OperationCanceledException)
                when (IsInitializationSuperseded(generation, cancellationToken))
            {
                return SupersededInitialization();
            }
        }

        public async Task<CommandOperationResult> SelectAsync(
            WebViewerSelectionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (!TryGetReadySelection(out CommandOperationResult failure))
            {
                return failure;
            }

            if (request == null)
            {
                return CommandOperationResult.Failure(
                    "invalid_selection",
                    "The selection payload is required.");
            }

            WebViewerSelectionResult result = selection.Select(
                request.Revision,
                request.ElementIds);
            if (!result.Applied)
            {
                return SelectionFailure(result);
            }

            TryAdvanceRevision(result.Revision);
            await eventPublisher.PublishAsync(
                "selection_applied",
                CreateSelectionEvent(result.Revision, selection.SelectedIds.Count, false),
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                CreateSelectionEvent(result.Revision, selection.SelectedIds.Count, false));
        }

        public async Task<CommandOperationResult> ClearAsync(
            WebViewerRevisionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (!TryGetReadySelection(out CommandOperationResult failure))
            {
                return failure;
            }

            if (request == null)
            {
                return CommandOperationResult.Failure(
                    "invalid_clear",
                    "The clear payload is required.");
            }

            WebViewerSelectionResult result = selection.Clear(request.Revision);
            if (!result.Applied)
            {
                return SelectionFailure(result);
            }

            TryAdvanceRevision(result.Revision);
            await eventPublisher.PublishAsync(
                "selection_applied",
                CreateSelectionEvent(result.Revision, 0, true),
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                CreateSelectionEvent(result.Revision, 0, true));
        }

        public async Task<CommandOperationResult> DisposeViewerAsync(
            WebViewerRevisionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (disposed)
            {
                return CommandOperationResult.Success(
                    new JObject { ["already_disposed"] = true });
            }

            if (request == null || !TryAdvanceRevision(request.Revision))
            {
                return CommandOperationResult.Failure(
                    "stale_revision",
                    "A newer disposal revision is required.");
            }

            DisposeCore();
            await eventPublisher.PublishAsync(
                "viewer_disposed",
                new JObject { ["revision"] = request.Revision },
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                new JObject { ["revision"] = request.Revision });
        }

        public void ReportLoadingProgress(float normalized, string message)
        {
            LoadingProgressChanged?.Invoke(Mathf.Clamp01(normalized), message ?? string.Empty);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                DisposeCore();
            }
        }

        private async Task<CommandOperationResult> FailInitializationAsync(
            long revision,
            string message,
            string remoteEndpoint,
            int generation,
            CancellationToken cancellationToken)
        {
            ResetCurrentModel();
            SetLifecycle(WebViewerLifecycleState.Failed);
            await eventPublisher.PublishAsync(
                "viewer_failed",
                new JObject
                {
                    ["revision"] = revision,
                    ["code"] = "initialization_failed",
                    ["message"] = message
                },
                remoteEndpoint,
                cancellationToken);
            if (!IsInitializationCurrent(generation, cancellationToken))
            {
                return SupersededInitialization();
            }

            return CommandOperationResult.Failure(
                "initialization_failed",
                message);
        }

        private bool IsInitializationCurrent(
            int generation,
            CancellationToken cancellationToken)
        {
            if (generation != Volatile.Read(ref initializationGeneration))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        private bool IsInitializationSuperseded(
            int generation,
            CancellationToken callerCancellationToken) =>
            generation != Volatile.Read(ref initializationGeneration) &&
            !callerCancellationToken.IsCancellationRequested;

        private bool TryAdvanceRevision(long revision)
        {
            while (true)
            {
                long current = Interlocked.Read(ref latestRevision);
                if (revision <= current)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(
                        ref latestRevision,
                        revision,
                        current) == current)
                {
                    return true;
                }
            }
        }

        public bool TryRecordRevision(long revision) =>
            TryAdvanceRevision(revision);

        private static CommandOperationResult SupersededInitialization() =>
            CommandOperationResult.Failure(
                "superseded",
                "The initialization was superseded.");

        private bool TryGetReadySelection(out CommandOperationResult failure)
        {
            if (disposed)
            {
                failure = CommandOperationResult.Failure(
                    "viewer_disposed",
                    "The viewer application is disposed.");
                return false;
            }

            if (Lifecycle != WebViewerLifecycleState.Ready || selection == null)
            {
                failure = CommandOperationResult.Failure(
                    "viewer_not_ready",
                    "Initialize the viewer before changing visibility.");
                return false;
            }

            failure = default;
            return true;
        }

        private static CommandOperationResult SelectionFailure(
            WebViewerSelectionResult result)
        {
            string code = result.Outcome == WebViewerSelectionOutcome.Stale
                ? "stale_revision"
                : "invalid_selection";
            return CommandOperationResult.Failure(code, result.Message);
        }

        private static JObject CreateSelectionEvent(
            long revision,
            int count,
            bool cleared)
        {
            return new JObject
            {
                ["revision"] = revision,
                ["selected_count"] = count,
                ["cleared"] = cleared
            };
        }

        private void ResetCurrentModel()
        {
            WebViewerModelContext model = CurrentModel;
            CurrentModel = null;
            if (model != null)
            {
                NotifyModelUnloading(model);
            }

            visibilityFeature?.Dispose();
            visibilityFeature = null;
            selection = null;
            modelLoader.Unload();
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }

            navigation.BeginReferenceLoad();
        }

        private void CancelInitialization()
        {
            if (initializationCancellation == null)
            {
                return;
            }

            initializationCancellation.Cancel();
            initializationCancellation.Dispose();
            initializationCancellation = null;
        }

        private void SetLifecycle(WebViewerLifecycleState value)
        {
            if (Lifecycle == value)
            {
                return;
            }

            Lifecycle = value;
            LifecycleChanged?.Invoke(value);
        }

        private void DisposeCore()
        {
            disposed = true;
            Interlocked.Increment(ref initializationGeneration);
            CancelInitialization();
            WebViewerModelContext model = CurrentModel;
            CurrentModel = null;
            if (model != null)
            {
                NotifyModelUnloading(model);
            }

            visibilityFeature?.Dispose();
            visibilityFeature = null;
            selection = null;
            modelLoader.Unload();
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }

            navigation.BeginReferenceLoad();
            modelLoader.Dispose();
            SetLifecycle(WebViewerLifecycleState.Disposed);
        }

        private bool TryCreateVisibilityFeature(
            WebViewerModelContext context,
            out IWebViewerVisibilityFeature feature,
            out string error)
        {
            if (visibilityFeatureFactory != null)
            {
                if (!visibilityFeatureFactory.TryCreate(
                        context,
                        out feature,
                        out error))
                {
                    feature?.Dispose();
                    feature = null;
                    error = string.IsNullOrWhiteSpace(error)
                        ? "The custom visibility feature could not be created."
                        : error.Trim();
                    return false;
                }

                if (feature == null)
                {
                    error = "The custom visibility factory returned no feature.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (!GenericWebViewerVisibilityFeature.TryCreate(
                    context,
                    out GenericWebViewerVisibilityFeature genericFeature,
                    out error))
            {
                feature = null;
                return false;
            }

            feature = genericFeature;
            return true;
        }

        private void NotifyModelReady(WebViewerModelContext context)
        {
            InvokeModelEvent(ModelReady, context);
        }

        private void NotifyModelUnloading(WebViewerModelContext context)
        {
            InvokeModelEvent(ModelUnloading, context);
        }

        private static void InvokeModelEvent(
            Action<WebViewerModelContext> handlers,
            WebViewerModelContext context)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<WebViewerModelContext> handler in
                     handlers.GetInvocationList())
            {
                try
                {
                    handler(context);
                }
                catch (Exception)
                {
                    // Product observers cannot invalidate the core model lifecycle.
                }
            }
        }
    }
}
