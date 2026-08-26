using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    public enum WebViewerLifecycleState
    {
        Created,
        Loading,
        Ready,
        Failed,
        Disposed
    }

    [Serializable]
    public sealed class WebViewerInitializeRequest
    {
        [JsonProperty("revision")]
        public long Revision { get; set; }

        [JsonProperty("model_url")]
        public string ModelUrl { get; set; }

        [JsonProperty("model_id")]
        public string ModelId { get; set; }

        [JsonProperty("model_version")]
        public string ModelVersion { get; set; }

        [JsonProperty("cache_version")]
        public uint? CacheVersion { get; set; }

        [JsonProperty("cache_hash")]
        public string CacheHash { get; set; }

        [JsonProperty("append_platform_query")]
        public bool AppendPlatformQuery { get; set; } = true;

        /// <summary>
        /// Product-resolved model placement. This is runtime composition state,
        /// not a second browser payload contract.
        /// </summary>
        [JsonIgnore]
        public WebViewerModelPlacement ModelPlacement { get; set; }

        /// <summary>
        /// Opts the composition root into centering the complete model bounds
        /// before product visibility and navigation are initialized.
        /// </summary>
        [JsonIgnore]
        public bool CenterModelOnWorldOrigin { get; set; }
    }

    public sealed class WebViewerModelPlacement
    {
        public WebViewerModelPlacement()
            : this(Vector3.zero, Vector3.zero, Vector3.one)
        {
        }

        public WebViewerModelPlacement(
            Vector3 position,
            Vector3 rotationEuler,
            Vector3 scale)
        {
            Position = position;
            RotationEuler = rotationEuler;
            Scale = scale;
        }

        public Vector3 Position { get; }
        public Vector3 RotationEuler { get; }
        public Vector3 Scale { get; }

        internal bool IsFinite() =>
            IsFinite(Position) &&
            IsFinite(RotationEuler) &&
            IsFinite(Scale);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class WebViewerSelectionRequest
    {
        [JsonProperty("revision")]
        public long Revision { get; set; }

        [JsonProperty("element_ids")]
        public List<string> ElementIds { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class WebViewerRevisionRequest
    {
        [JsonProperty("revision")]
        public long Revision { get; set; }
    }

    public readonly struct WebViewerModelDescriptor
    {
        public WebViewerModelDescriptor(
            string sourceUrl,
            string modelId,
            string modelVersion,
            uint? cacheVersion,
            string cacheHash,
            bool appendPlatformQuery)
        {
            SourceUrl = Normalize(sourceUrl);
            ModelId = Normalize(modelId);
            ModelVersion = Normalize(modelVersion);
            CacheVersion = cacheVersion;
            CacheHash = Normalize(cacheHash);
            AppendPlatformQuery = appendPlatformQuery;
        }

        public string SourceUrl { get; }
        public string ModelId { get; }
        public string ModelVersion { get; }
        public uint? CacheVersion { get; }
        public string CacheHash { get; }
        public bool AppendPlatformQuery { get; }
        public bool UsesEmbeddedModel => SourceUrl.Length == 0;

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class WebViewerModelLoadResult
    {
        private WebViewerModelLoadResult(
            bool succeeded,
            GameObject referenceRoot,
            string message)
        {
            Succeeded = succeeded;
            ReferenceRoot = referenceRoot;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public GameObject ReferenceRoot { get; }
        public string Message { get; }

        public static WebViewerModelLoadResult Success(GameObject root) =>
            new WebViewerModelLoadResult(true, root, string.Empty);

        public static WebViewerModelLoadResult Failure(string message) =>
            new WebViewerModelLoadResult(false, null, message);
    }

    public interface IWebViewerModelDescriptorResolver
    {
        bool TryResolve(
            WebViewerInitializeRequest request,
            out WebViewerModelDescriptor descriptor,
            out string error);
    }

    public interface IWebViewerModelLoader : IDisposable
    {
        Task<WebViewerModelLoadResult> LoadAsync(
            WebViewerModelDescriptor descriptor,
            CancellationToken cancellationToken);

        void Unload();
    }

    public interface IWebViewerEventPublisher
    {
        Task PublishAsync(
            string eventName,
            JObject payload,
            string remoteEndpoint,
            CancellationToken cancellationToken = default);
    }
}
