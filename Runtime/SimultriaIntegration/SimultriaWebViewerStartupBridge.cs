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
        [SerializeField] private SimultriaViewerBuildConnectionGate connectionGate;
        [SerializeField] private WebViewerBootstrap viewerBootstrap;
        private IDisposable subscription;

        private void OnEnable()
        {
            Detach();
            try
            {
                if (viewerBootstrap == null || gameObject.scene != viewerBootstrap.gameObject.scene)
                {
                    throw new ArgumentException("The startup bridge requires an explicit same-scene viewer.");
                }

                subscription = new SimultriaWebViewerStartupSubscription(
                    connectionGate, viewerBootstrap, WebViewerStartupStatusProjection.Publish);
            }
            catch (ArgumentException)
            {
                DeucarianWebGLShell.ReportState(
                    DeucarianWebGLShellState.Failed, "viewer_connection_failed");
            }
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
