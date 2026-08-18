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
        private CancellationTokenSource initializationCancellation;
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
            IViewerAuthenticationSession viewerAuthentication = null)
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
            Lifecycle = WebViewerLifecycleState.Created;
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }
        }

        public event Action<WebViewerLifecycleState> LifecycleChanged;
        public event Action<float, string> LoadingProgressChanged;

        public WebViewerLifecycleState Lifecycle { get; private set; }
        public long LatestRevision => Interlocked.Read(ref latestRevision);
        public int IndexedElementCount { get; private set; }
        public int SelectedElementCount => selection?.SelectedIds.Count ?? 0;
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

                if (!WebViewerElementIndex.TryCreate(
                        referenceRoot,
                        out WebViewerElementIndex index,
                        out string indexError))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        indexError,
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                var visibility = new WebViewerVisibilityController(index);
                selection = new WebViewerSelectionStateOwner(request.Revision, visibility);
                IndexedElementCount = index.Count;
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
            selection = null;
            IndexedElementCount = 0;
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
            selection = null;
            IndexedElementCount = 0;
            modelLoader.Unload();
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }

            navigation.BeginReferenceLoad();
            modelLoader.Dispose();
            SetLifecycle(WebViewerLifecycleState.Disposed);
        }
    }
}
