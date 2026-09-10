using System;
using Deucarian.SimultriaViewerIntegration;
using Deucarian.WebGLTemplate;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.SimultriaIntegration
{
    /// <summary>
    /// Optional, explicit projection of one connection gate into its Web page.
    /// It observes routing only and never enables the viewer or chooses a backend.
    /// Keep this component enabled while the gate holds its bootstrap disabled.
    /// </summary>
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class SimultriaWebViewerStartupBridge : MonoBehaviour
    {
        internal const string InvalidBindingShellCode = "viewer_composition_failed";
        [SerializeField] private SimultriaViewerBuildConnectionGate connectionGate;
        [SerializeField] private WebViewerBootstrap viewerBootstrap;
        private IDisposable subscription;

        /// <summary>The last binding attempt's fixed local diagnostic reason.</summary>
        public SimultriaWebViewerStartupBindingFailure BindingFailure { get; private set; }

        private void OnEnable()
        {
            Detach();
            BindingFailure = SimultriaWebViewerStartupSubscription.ValidateBinding(
                connectionGate, viewerBootstrap);
            if (BindingFailure == SimultriaWebViewerStartupBindingFailure.None &&
                gameObject.scene != viewerBootstrap.gameObject.scene)
            {
                BindingFailure = SimultriaWebViewerStartupBindingFailure.SceneMismatch;
            }

            if (BindingFailure != SimultriaWebViewerStartupBindingFailure.None)
            {
                // Invalid scene wiring is configuration failure, not a claim
                // that a backend connection attempt failed. Keep page copy safe.
                DeucarianWebGLShell.ReportState(
                    DeucarianWebGLShellState.Failed, InvalidBindingShellCode);
                return;
            }

            subscription = new SimultriaWebViewerStartupSubscription(
                connectionGate, viewerBootstrap, WebViewerStartupStatusProjection.Publish);
        }

        private void OnDisable() => Detach();
        private void OnDestroy() => Detach();

        private void Detach()
        {
            subscription?.Dispose();
            subscription = null;
        }
    }
}
