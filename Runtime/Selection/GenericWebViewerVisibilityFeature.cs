using System;

namespace Deucarian.TemplateViewerWeb.Selection
{
    internal sealed class GenericWebViewerVisibilityFeature :
        IWebViewerVisibilityFeature
    {
        private readonly WebViewerVisibilityController visibility;
        private bool disposed;

        private GenericWebViewerVisibilityFeature(
            WebViewerElementIndex index,
            long initialRevision)
        {
            visibility = new WebViewerVisibilityController(index);
            Selection = new WebViewerSelectionStateOwner(
                initialRevision,
                visibility);
            IndexedElementCount = index.Count;
        }

        public int IndexedElementCount { get; }
        public int SelectedElementCount => Selection.SelectedIds.Count;
        public WebViewerSelectionStateOwner Selection { get; }

        public static bool TryCreate(
            WebViewerModelContext context,
            out GenericWebViewerVisibilityFeature feature,
            out string error)
        {
            feature = null;
            if (!WebViewerElementIndex.TryCreate(
                    context.ReferenceRoot,
                    out WebViewerElementIndex index,
                    out error))
            {
                return false;
            }

            feature = new GenericWebViewerVisibilityFeature(
                index,
                context.InitialRevision);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            visibility.RestoreBaseline();
            disposed = true;
        }
    }
}
