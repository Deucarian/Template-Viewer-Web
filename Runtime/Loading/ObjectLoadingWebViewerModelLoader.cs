using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;
using Deucarian.ObjectLoading.APIIntegration;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Loading
{
    public sealed class ObjectLoadingWebViewerModelLoader :
        IWebViewerModelLoader
    {
        private readonly MonoBehaviour coroutineOwner;
        private readonly IApiClient apiClient;
        private readonly Transform modelParent;
        private readonly string apiBaseUrl;
        private readonly IReadOnlyCollection<string> authenticatedModelOrigins;
        private ObjectLoadingPipeline activePipeline;
        private CancellationTokenSource activeCancellation;
        private int generation;
        private bool disposed;

        public ObjectLoadingWebViewerModelLoader(
            MonoBehaviour owner,
            IApiClient client,
            Transform parent)
            : this(owner, client, parent, null, null)
        {
        }

        public ObjectLoadingWebViewerModelLoader(
            MonoBehaviour owner,
            IApiClient client,
            Transform parent,
            string configuredApiBaseUrl,
            IEnumerable<string> additionalAuthenticatedOrigins)
        {
            coroutineOwner = owner ?? throw new ArgumentNullException(nameof(owner));
            apiClient = client ?? throw new ArgumentNullException(nameof(client));
            modelParent = parent ?? throw new ArgumentNullException(nameof(parent));
            apiBaseUrl = configuredApiBaseUrl;
            authenticatedModelOrigins = additionalAuthenticatedOrigins == null
                ? Array.Empty<string>()
                : new List<string>(additionalAuthenticatedOrigins);
        }

        public event Action<float, string> ProgressChanged;

        public Task<WebViewerModelLoadResult> LoadAsync(
            WebViewerModelDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (descriptor.UsesEmbeddedModel)
            {
                return Task.FromResult(WebViewerModelLoadResult.Failure(
                    "Object Loading requires a source URL."));
            }

            Unload();
            int loadGeneration = generation;
            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            CancellationToken token = activeCancellation.Token;
            ApiAuthenticationRequirement providerAuthentication =
                WebViewerModelAuthenticationPolicy.Resolve(
                    descriptor.SourceUrl,
                    apiBaseUrl,
                    authenticatedModelOrigins);
            ObjectLoadingPipeline pipeline =
                ApiObjectLoadingPipelineFactory.Create(
                    apiClient,
                    providerAuthentication);
            var completion = new TaskCompletionSource<WebViewerModelLoadResult>();

            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(descriptor.SourceUrl);
            request.Parent = modelParent;
            request.DisplayName = descriptor.ModelId.Length > 0
                ? "Web viewer model " + descriptor.ModelId
                : "Web viewer model";
            request.CacheKey = descriptor.ModelId.Length > 0
                ? descriptor.ModelId + ":" + descriptor.ModelVersion
                : null;
            request.CacheVersion = descriptor.CacheVersion;
            request.CacheHash = descriptor.CacheHash.Length > 0
                ? descriptor.CacheHash
                : null;
            request.AppendPlatformQuery = descriptor.AppendPlatformQuery;
            request.CancellationToken = token;
            request.Progress = progress => ProgressChanged?.Invoke(
                progress?.Normalized ?? 0f,
                progress?.Message ?? string.Empty);

            coroutineOwner.StartCoroutine(pipeline.LoadAsync(request, result =>
            {
                if (disposed || loadGeneration != generation || token.IsCancellationRequested)
                {
                    pipeline.UnloadLast();
                    completion.TrySetResult(WebViewerModelLoadResult.Failure(
                        "The model load was superseded."));
                    return;
                }

                if (result == null || !result.Succeeded || result.Handle == null)
                {
                    pipeline.UnloadLast();
                    completion.TrySetResult(WebViewerModelLoadResult.Failure(
                        result?.Message ?? "The model did not load."));
                    return;
                }

                activePipeline = pipeline;
                GameObject referenceRoot = result.Handle.InstantiatedObjects.Count == 1
                    ? result.Handle.InstantiatedObjects[0]
                    : modelParent.gameObject;
                completion.TrySetResult(WebViewerModelLoadResult.Success(referenceRoot));
            }));
            return completion.Task;
        }

        public void Unload()
        {
            generation++;
            if (activeCancellation != null)
            {
                activeCancellation.Cancel();
                activeCancellation.Dispose();
                activeCancellation = null;
            }

            activePipeline?.UnloadLast();
            activePipeline = null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Unload();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
