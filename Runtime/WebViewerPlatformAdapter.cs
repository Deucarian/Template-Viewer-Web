using System;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.Diagnostics;
using Deucarian.TemplateViewer;
using Deucarian.TemplateViewerWeb.Commands;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    internal sealed class WebViewerPlatformAdapter : IViewerPlatformAdapter
    {
        private readonly GameObject host;
        private readonly WebGlCommandTransportOptions options;
        private readonly WebGlWebViewerEventPublisher eventPublisher;
        private readonly WebViewerLifecycleStatusSink lifecycleStatusSink;
        private ActivationLease activation;
        private bool disposed;

        internal WebViewerPlatformAdapter(
            GameObject hostObject,
            WebGlCommandTransportOptions transportOptions)
        {
            host = hostObject ??
                throw new ArgumentNullException(nameof(hostObject));
            options = transportOptions ??
                throw new ArgumentNullException(nameof(transportOptions));
            eventPublisher = new WebGlWebViewerEventPublisher();
            lifecycleStatusSink = new WebViewerLifecycleStatusSink();
        }

        public string PlatformId => "webgl";

        public string EventEndpoint =>
            options.Mode == WebGlCommandTransportMode.ParentIframe
                ? "parent:" + options.TargetOrigin
                : "direct";

        public IViewerEventPublisher EventPublisher => eventPublisher;
        public IViewerLifecycleStatusSink LifecycleStatusSink =>
            lifecycleStatusSink;

        public IDisposable ActivateCommandTransport(
            CommandRoutingRuntime<ViewerApplication> commandRuntime)
        {
            if (commandRuntime == null)
            {
                throw new ArgumentNullException(nameof(commandRuntime));
            }

            ThrowIfDisposed();
            if (activation != null)
            {
                throw new InvalidOperationException(
                    "The browser command transport is already active.");
            }

            var transport = new WebGlCommandTransport(options);
            var diagnostics = new WebViewerPlatformDiagnosticProvider(options);
            DiagnosticProviderRegistration diagnosticRegistration =
                DiagnosticProviderRegistry.Register(diagnostics);
            var bridge = new CommandTransportBridge<ViewerApplication>(
                commandRuntime,
                transport,
                shouldSendResponses: true,
                disposeTransport: true);
            try
            {
                WebGlCommandTransportBehaviour behaviour =
                    host.AddComponent<WebGlCommandTransportBehaviour>();
                behaviour.Initialize(transport);
                eventPublisher.Attach(transport);
                bridge.Start();
                diagnostics.SetActive(true);
                activation = new ActivationLease(
                    this,
                    transport,
                    bridge,
                    diagnostics,
                    diagnosticRegistration);
                return activation;
            }
            catch
            {
                eventPublisher.Detach(transport);
                try
                {
                    bridge.Dispose();
                }
                catch (Exception)
                {
                    // Preserve the activation failure after best-effort
                    // transport cleanup.
                }

                try
                {
                    diagnosticRegistration.Dispose();
                }
                catch (Exception)
                {
                    // Preserve the activation failure after best-effort
                    // diagnostics cleanup.
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            activation?.Dispose();
            activation = null;
        }

        private void Release(ActivationLease lease)
        {
            if (ReferenceEquals(activation, lease))
            {
                activation = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private sealed class ActivationLease : IDisposable
        {
            private readonly WebViewerPlatformAdapter owner;
            private readonly WebGlCommandTransport transport;
            private readonly CommandTransportBridge<ViewerApplication> bridge;
            private readonly WebViewerPlatformDiagnosticProvider diagnostics;
            private readonly DiagnosticProviderRegistration
                diagnosticRegistration;
            private bool disposed;

            internal ActivationLease(
                WebViewerPlatformAdapter adapter,
                WebGlCommandTransport commandTransport,
                CommandTransportBridge<ViewerApplication> commandBridge,
                WebViewerPlatformDiagnosticProvider diagnosticProvider,
                DiagnosticProviderRegistration registration)
            {
                owner = adapter;
                transport = commandTransport;
                bridge = commandBridge;
                diagnostics = diagnosticProvider;
                diagnosticRegistration = registration;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                diagnostics.SetActive(false);
                try
                {
                    bridge.Dispose();
                }
                finally
                {
                    owner.eventPublisher.Detach(transport);
                    diagnosticRegistration.Dispose();
                    owner.Release(this);
                }
            }
        }
    }
}
