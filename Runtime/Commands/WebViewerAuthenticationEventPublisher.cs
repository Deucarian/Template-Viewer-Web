using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.ViewerAuthentication;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewerWeb.Commands
{
    /// <summary>
    /// Adapts sanitized Viewer Authentication outcomes to the template's
    /// existing browser event publisher.
    /// </summary>
    public sealed class WebViewerAuthenticationEventPublisher :
        IViewerAuthenticationEventPublisher
    {
        private readonly IWebViewerEventPublisher eventPublisher;
        private readonly string remoteEndpoint;

        public WebViewerAuthenticationEventPublisher(
            IWebViewerEventPublisher publisher,
            string configuredRemoteEndpoint)
        {
            eventPublisher = publisher ??
                throw new ArgumentNullException(nameof(publisher));
            remoteEndpoint = string.IsNullOrWhiteSpace(configuredRemoteEndpoint)
                ? throw new ArgumentException(
                    "A configured browser endpoint is required.",
                    nameof(configuredRemoteEndpoint))
                : configuredRemoteEndpoint.Trim();
        }

        public Task PublishAsync(
            string eventName,
            ViewerAuthenticationStatusSnapshot status,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var payload = new JObject
            {
                ["status"] = status.Status.ToString(),
                ["has_access_token"] = status.HasAccessToken,
                ["can_refresh"] = status.CanRefresh,
                ["expiry_known"] = status.ExpiresAtUtc.HasValue
            };
            if (status.ExpiresAtUtc.HasValue)
            {
                payload["expires_at_utc"] =
                    status.ExpiresAtUtc.Value.ToUniversalTime().ToString(
                        "O",
                        CultureInfo.InvariantCulture);
            }

            return eventPublisher.PublishAsync(
                eventName,
                payload,
                remoteEndpoint,
                cancellationToken);
        }
    }
}
