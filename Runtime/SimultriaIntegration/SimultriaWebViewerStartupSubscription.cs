using System;
using Deucarian.SimultriaViewerIntegration;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration
{
    internal sealed class SimultriaWebViewerStartupSubscription : IDisposable
    {
        private readonly SimultriaViewerBuildConnectionGate gate;
        private readonly Action<WebViewerStartupStatusProjection> publish;
        private bool disposed;

        internal SimultriaWebViewerStartupSubscription(
            SimultriaViewerBuildConnectionGate connectionGate,
            WebViewerBootstrap viewer,
            Action<WebViewerStartupStatusProjection> publisher)
        {
            if (ValidateBinding(connectionGate, viewer) != SimultriaWebViewerStartupBindingFailure.None)
            {
                throw new ArgumentException("The connection gate must explicitly own this same-scene viewer.");
            }

            gate = connectionGate;
            publish = publisher ?? throw new ArgumentNullException(nameof(publisher));
            gate.StartupStatusChanged += OnStatus;
            // Subscribe before reading the cached state: an early gate failure
            // cannot be missed while the bootstrap/transport is still disabled.
            try
            {
                OnStatus(gate.StartupStatus);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal static SimultriaWebViewerStartupBindingFailure ValidateBinding(
            SimultriaViewerBuildConnectionGate connectionGate, WebViewerBootstrap viewer)
        {
            if (viewer == null) return SimultriaWebViewerStartupBindingFailure.ViewerMissing;
            if (connectionGate == null) return SimultriaWebViewerStartupBindingFailure.ConnectionGateMissing;
            // Unity sets isLoaded only after scene objects receive OnEnable.
            // Explicit live references and valid scene identity already prove
            // the binding; requiring isLoaded rejects genuine cold activation.
            if (!viewer.gameObject.scene.IsValid() || !connectionGate.gameObject.scene.IsValid())
                return SimultriaWebViewerStartupBindingFailure.InvalidScene;
            if (connectionGate.gameObject.scene != viewer.gameObject.scene)
                return SimultriaWebViewerStartupBindingFailure.SceneMismatch;
            if (!connectionGate.ContainsStartupBehaviour(viewer))
                return SimultriaWebViewerStartupBindingFailure.ViewerNotOwned;
            return SimultriaWebViewerStartupBindingFailure.None;
        }

        internal void OnStatus(SimultriaViewerBuildStartupSnapshot snapshot)
        {
            if (disposed) return;
            if (snapshot?.Phase == SimultriaViewerBuildStartupPhase.Disposed)
            {
                Dispose();
                return;
            }

            publish(WebViewerStartupStatusProjection.From(snapshot));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Unity's destroyed-object equality must not suppress CLR event
            // cleanup while the gate is publishing its terminal snapshot.
            if (!ReferenceEquals(gate, null)) gate.StartupStatusChanged -= OnStatus;
        }
    }
}
