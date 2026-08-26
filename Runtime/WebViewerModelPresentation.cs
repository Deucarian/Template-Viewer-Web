using Deucarian.ViewerNavigation;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    internal static class WebViewerModelPresentation
    {
        internal static bool TryPrepare(
            Transform referenceRoot,
            WebViewerInitializeRequest request,
            out string error)
        {
            if (referenceRoot == null)
            {
                error = "The model presentation root is missing.";
                return false;
            }

            WebViewerModelPlacement placement = request?.ModelPlacement;
            if (placement != null)
            {
                if (!placement.IsFinite())
                {
                    error = "The model placement contains invalid values.";
                    return false;
                }

                referenceRoot.localPosition = Vector3.zero;
                referenceRoot.localRotation = Quaternion.identity;
                referenceRoot.localScale = Vector3.one;
                referenceRoot.localPosition = placement.Position;
                referenceRoot.localRotation =
                    Quaternion.Euler(placement.RotationEuler);
                referenceRoot.localScale = placement.Scale;
            }

            if (request?.CenterModelOnWorldOrigin != true)
            {
                error = string.Empty;
                return true;
            }

            ViewerNavigationReferenceCenteringResult result =
                ViewerNavigationReferenceCentering
                    .CenterMeshRendererBoundsAtWorldOrigin(
                        referenceRoot,
                        true);
            if (!result.Applied)
            {
                error = string.IsNullOrWhiteSpace(result.Message)
                    ? "The model bounds could not be centered."
                    : result.Message;
                return false;
            }

            if (!result.IsCenteredAtWorldOrigin)
            {
                error = "The model bounds did not center on the world origin.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
