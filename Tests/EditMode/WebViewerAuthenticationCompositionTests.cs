using System.Reflection;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API;
using Deucarian.Session.APIIntegration;
using Deucarian.ViewerAuthentication;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerAuthenticationCompositionTests
    {
        [Test]
        public void BootstrapComposesTheSharedEndpointProviderFromItsProfile()
        {
            GameObject root = new GameObject("Auth Composition Test");
            SessionTokenEndpointProfile profile = null;
            ApiClientConfig apiClientConfig = null;
            try
            {
                profile = SessionTokenEndpointProfile.CreateRuntime(
                    new SessionTokenEndpointConfig(
                        "https://auth.example.invalid/login",
                        new[]
                        {
                            new SessionTokenEndpointInputDefinition(
                                "identity",
                                "identity",
                                "Identity"),
                            new SessionTokenEndpointInputDefinition(
                                "password",
                                "password",
                                "Password",
                                SessionTokenEndpointInputPlacement.JsonBody,
                                isSecret: true)
                        },
                        new SessionTokenEndpointResponseMapping(
                            "access_token"),
                        HttpMethod.POST));
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                typeof(WebViewerBootstrap).GetField(
                        "authenticationTokenEndpointProfile",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(bootstrap, profile);

                MethodInfo createProvider = typeof(WebViewerBootstrap)
                    .GetMethod(
                        "CreateAuthenticationAcquisitionProvider",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                apiClientConfig = ApiClientConfig.CreateRuntimeDefault();
                var provider = createProvider?.Invoke(
                    bootstrap,
                    new object[]
                    {
                        ApiClientFactory.Create(apiClientConfig)
                    }) as
                    IInteractiveViewerAuthenticationAcquisitionProvider;

                Assert.That(
                    bootstrap.ResolvedAuthenticationTokenEndpointProfile,
                    Is.SameAs(profile));
                Assert.That(provider, Is.Not.Null);
                Assert.That(provider.DisplayName, Is.EqualTo("Refresh Token"));
                Assert.That(provider.InputDescriptors.Count, Is.EqualTo(2));
                Assert.That(provider.InputDescriptors[1].IsSecret, Is.True);
            }
            finally
            {
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }

                if (apiClientConfig != null)
                {
                    Object.DestroyImmediate(apiClientConfig);
                }

                Object.DestroyImmediate(root);
            }
        }
    }
}
