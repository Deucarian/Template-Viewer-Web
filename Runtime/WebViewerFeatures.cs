using System;
using System.Collections.Generic;
using Deucarian.CommandRouting;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    public sealed class WebViewerModelContext
    {
        public WebViewerModelContext(
            GameObject referenceRoot,
            WebViewerModelDescriptor descriptor,
            long initialRevision)
        {
            ReferenceRoot = referenceRoot ??
                throw new ArgumentNullException(nameof(referenceRoot));
            Descriptor = descriptor;
            InitialRevision = initialRevision;
        }

        public GameObject ReferenceRoot { get; }
        public WebViewerModelDescriptor Descriptor { get; }
        public long InitialRevision { get; }
    }

    /// <summary>
    /// Owns every visibility change for one loaded model. A product may replace
    /// the template's generic element selection by supplying one factory.
    /// </summary>
    public interface IWebViewerVisibilityFeature : IDisposable
    {
        int IndexedElementCount { get; }
        int SelectedElementCount { get; }
    }

    public interface IWebViewerVisibilityFeatureFactory
    {
        bool TryCreate(
            WebViewerModelContext context,
            out IWebViewerVisibilityFeature feature,
            out string error);
    }

    /// <summary>
    /// Scene-local extension point for product commands and product visibility.
    /// Add derived components beside WebViewerBootstrap.
    /// </summary>
    public abstract class WebViewerFeatureBehaviour : MonoBehaviour
    {
        public virtual IWebViewerVisibilityFeatureFactory
            VisibilityFeatureFactory => null;

        public virtual IReadOnlyList<ICommandHandler<WebViewerApplication>>
            CreateCommandHandlers() =>
                Array.Empty<ICommandHandler<WebViewerApplication>>();

        public virtual void Attach(WebViewerApplication application)
        {
        }

        public virtual void Detach(WebViewerApplication application)
        {
        }
    }
}
