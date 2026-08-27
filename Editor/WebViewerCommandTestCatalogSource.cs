using System;
using System.Collections.Generic;
using Deucarian.CommandRouting.Editor;
using Deucarian.TemplateViewer.Commands;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Editor
{
    [InitializeOnLoad]
    public sealed class WebViewerCommandTestCatalogSource :
        ICommandTestCatalogSource
    {
        public const string SourceId = "deucarian.web-viewer";

        static WebViewerCommandTestCatalogSource()
        {
            CommandTestCatalogSourceRegistry.Register(
                new WebViewerCommandTestCatalogSource());
        }

        public string Id => SourceId;
        public string DisplayName => "Web Viewer";

        public bool TryGetCatalogJson(out string json, out string error)
        {
            json = string.Empty;
            WebViewerBootstrap[] candidates =
                Resources.FindObjectsOfTypeAll<WebViewerBootstrap>();
            var sceneBootstraps = new List<WebViewerBootstrap>();
            for (int index = 0; index < candidates.Length; index++)
            {
                WebViewerBootstrap candidate = candidates[index];
                if (candidate == null ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded ||
                    EditorUtility.IsPersistent(candidate))
                {
                    continue;
                }

                sceneBootstraps.Add(candidate);
            }

            if (sceneBootstraps.Count != 1)
            {
                error = sceneBootstraps.Count == 0
                    ? "Open a scene containing one WebViewerBootstrap."
                    : "The loaded scenes contain " + sceneBootstraps.Count +
                      " WebViewerBootstrap components; exactly one is required.";
                return false;
            }

            try
            {
                ViewerCommandHarnessCatalog catalog =
                    WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                        sceneBootstraps[0]);
                WebViewerBootstrap bootstrap = sceneBootstraps[0];
                string remoteEndpoint = bootstrap.IframeMode
                    ? "parent:" + bootstrap.ParentOrigin
                    : "direct";
                json = JsonConvert.SerializeObject(
                    new
                    {
                        schema_version = catalog.SchemaVersion,
                        remote_endpoint = remoteEndpoint,
                        default_scenario_id = catalog.DefaultScenarioId,
                        scenarios = catalog.Scenarios
                    },
                    Formatting.Indented);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "The Web Viewer command catalog could not be created: " +
                        exception.Message;
                return false;
            }
        }
    }
}
