using System;
using System.Collections.Generic;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewerWeb.Commands;
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
        /// <summary>
        /// Replaces the generic initialize_viewer handler when a product needs
        /// to resolve a typed project/model context before model loading.
        /// </summary>
        public virtual ICommandHandler<WebViewerApplication>
            InitializationCommandHandler => null;

        public virtual IWebViewerVisibilityFeatureFactory
            VisibilityFeatureFactory => null;

        public virtual IReadOnlyList<ICommandHandler<WebViewerApplication>>
            CreateCommandHandlers() =>
                Array.Empty<ICommandHandler<WebViewerApplication>>();

        /// <summary>
        /// Supplies safe local-browser examples for commands contributed by
        /// this feature. The harness catalog still includes every registered
        /// command when no example is supplied, but leaves it out of the
        /// automatic run until a representative payload is available.
        /// </summary>
        public virtual IReadOnlyList<WebViewerCommandHarnessScenario>
            CreateCommandHarnessScenarios() =>
                Array.Empty<WebViewerCommandHarnessScenario>();

        public virtual void Attach(WebViewerApplication application)
        {
        }

        public virtual void Detach(WebViewerApplication application)
        {
        }
    }

    public static class WebViewerFeatureComposition
    {
        public static ICommandHandler<WebViewerApplication>
            ResolveInitializationCommandHandler(
                IReadOnlyList<WebViewerFeatureBehaviour> features)
        {
            ICommandHandler<WebViewerApplication> result = null;
            if (features == null)
            {
                return null;
            }

            for (int index = 0; index < features.Count; index++)
            {
                ICommandHandler<WebViewerApplication> candidate =
                    features[index]?.InitializationCommandHandler;
                if (candidate == null)
                {
                    continue;
                }

                if (result != null && !ReferenceEquals(result, candidate))
                {
                    throw new InvalidOperationException(
                        "Only one viewer feature may own initialization.");
                }

                if (!HandlesInitialization(candidate))
                {
                    throw new InvalidOperationException(
                        "A product initialization handler must handle only " +
                        InitializeWebViewerCommandHandler.CommandName + ".");
                }

                result = candidate;
            }

            return result;
        }

        private static bool HandlesInitialization(
            ICommandHandler<WebViewerApplication> handler)
        {
            IReadOnlyList<string> names = handler.CommandNames;
            return names != null &&
                   names.Count == 1 &&
                   string.Equals(
                       names[0],
                       InitializeWebViewerCommandHandler.CommandName,
                       StringComparison.Ordinal);
        }
    }
}
