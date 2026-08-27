using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.TemplateViewer;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewerWeb.Commands
{
    public sealed class WebGlWebViewerEventPublisher : IViewerEventPublisher
    {
        private WebGlCommandTransport transport;

        internal WebGlWebViewerEventPublisher()
        {
        }

        public WebGlWebViewerEventPublisher(WebGlCommandTransport commandTransport)
        {
            transport = commandTransport ??
                throw new ArgumentNullException(nameof(commandTransport));
        }

        internal void Attach(WebGlCommandTransport commandTransport)
        {
            if (commandTransport == null)
            {
                throw new ArgumentNullException(nameof(commandTransport));
            }

            if (transport != null &&
                !ReferenceEquals(transport, commandTransport))
            {
                throw new InvalidOperationException(
                    "The browser event publisher is already attached.");
            }

            transport = commandTransport;
        }

        internal void Detach(WebGlCommandTransport commandTransport)
        {
            if (ReferenceEquals(transport, commandTransport))
            {
                transport = null;
            }
        }

        public Task PublishAsync(
            string eventName,
            JObject payload,
            string remoteEndpoint,
            CancellationToken cancellationToken = default)
        {
            WebGlCommandTransport current = transport;
            if (current == null)
            {
                throw new InvalidOperationException(
                    "The browser event publisher is not active.");
            }

            return current.PublishEventAsync(
                eventName,
                payload,
                remoteEndpoint,
                cancellationToken);
        }
    }
}
