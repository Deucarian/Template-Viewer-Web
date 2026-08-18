using System;
using System.Collections.Generic;
using Deucarian.API.Models;

namespace Deucarian.TemplateViewerWeb.Loading
{
    /// <summary>
    /// Limits live session authentication to API-relative or explicitly trusted
    /// model origins. This prevents a host-supplied cross-origin model URL from
    /// receiving the viewer credential by default.
    /// </summary>
    public static class WebViewerModelAuthenticationPolicy
    {
        public static ApiAuthenticationRequirement Resolve(
            string modelUrl,
            string apiBaseUrl,
            IEnumerable<string> additionalAuthenticatedOrigins = null)
        {
            if (string.IsNullOrWhiteSpace(modelUrl))
            {
                return ApiAuthenticationRequirement.Disabled;
            }

            string candidate = modelUrl.Trim();
            if (!Uri.TryCreate(candidate, UriKind.RelativeOrAbsolute, out Uri modelUri))
            {
                return ApiAuthenticationRequirement.Disabled;
            }

            if (!modelUri.IsAbsoluteUri)
            {
                return candidate.StartsWith("//", StringComparison.Ordinal)
                    ? ApiAuthenticationRequirement.Disabled
                    : ApiAuthenticationRequirement.Optional;
            }

            if (!IsHttp(modelUri))
            {
                return ApiAuthenticationRequirement.Disabled;
            }

            if (TryGetAbsoluteHttpUri(apiBaseUrl, out Uri baseUri) &&
                HasSameOrigin(modelUri, baseUri))
            {
                return ApiAuthenticationRequirement.Optional;
            }

            if (additionalAuthenticatedOrigins != null)
            {
                foreach (string origin in additionalAuthenticatedOrigins)
                {
                    if (TryGetExactOrigin(origin, out Uri trustedOrigin) &&
                        HasSameOrigin(modelUri, trustedOrigin))
                    {
                        return ApiAuthenticationRequirement.Optional;
                    }
                }
            }

            return ApiAuthenticationRequirement.Disabled;
        }

        private static bool TryGetAbsoluteHttpUri(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && IsHttp(uri);
        }

        private static bool TryGetExactOrigin(string value, out Uri uri)
        {
            if (!TryGetAbsoluteHttpUri(value, out uri))
            {
                return false;
            }

            return uri.AbsolutePath == "/" &&
                   string.IsNullOrEmpty(uri.Query) &&
                   string.IsNullOrEmpty(uri.Fragment) &&
                   string.IsNullOrEmpty(uri.UserInfo);
        }

        private static bool IsHttp(Uri uri) =>
            uri != null &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static bool HasSameOrigin(Uri left, Uri right) =>
            left != null &&
            right != null &&
            string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            left.Port == right.Port;
    }
}
