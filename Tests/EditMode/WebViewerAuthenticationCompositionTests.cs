using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API;
using Deucarian.Session.APIIntegration;
using Deucarian.Authentication;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerAuthenticationCompositionTests
    {
        [Test]
        public void BootstrapDefersCompositionForRuntimeConnectionRegistration()
        {
            var root = new GameObject("Deferred viewer");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                MethodInfo start = typeof(WebViewerBootstrap).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(start, Is.Not.Null);
                Assert.That(
                    typeof(IEnumerator).IsAssignableFrom(start.ReturnType),
                    Is.True);
                var routine = (IEnumerator)start.Invoke(bootstrap, null);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(bootstrap.Application, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(ViewerRuntimeConnectionResolutionStatus.None, true)]
        [TestCase(ViewerRuntimeConnectionResolutionStatus.Resolved, false)]
        public void LocalAuthenticationIsUsedOnlyWhenNoConnectionProviderExists(
            ViewerRuntimeConnectionResolutionStatus status,
            bool expectedLocalFallback)
        {
            MethodInfo method = GetPrivateMethod(
                "ShouldUseLocalAuthentication");

            Assert.That(
                method.Invoke(null, new object[] { status }),
                Is.EqualTo(expectedLocalFallback));
        }

        [TestCase(ViewerRuntimeConnectionResolutionStatus.Failed)]
        [TestCase(ViewerRuntimeConnectionResolutionStatus.Ambiguous)]
        public void BrokenConnectionResolutionFailsClosed(
            ViewerRuntimeConnectionResolutionStatus status)
        {
            MethodInfo method = GetPrivateMethod(
                "ShouldUseLocalAuthentication");

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => method.Invoke(null, new object[] { status }));

            Assert.That(
                exception?.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ConnectionAndTemplateAuthenticatedOriginsAreCombined()
        {
            GameObject root = new GameObject("Auth Origins Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                FieldInfo originsField = typeof(WebViewerBootstrap).GetField(
                    "authenticatedModelOrigins",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var localOrigins = originsField?.GetValue(bootstrap) as
                    List<string>;
                Assert.That(localOrigins, Is.Not.Null);
                localOrigins.Add("https://assets.example.invalid");

                MethodInfo merge = GetPrivateMethod(
                    "MergeAuthenticatedOrigins");
                var resolved = merge.Invoke(
                    bootstrap,
                    new object[]
                    {
                        new[]
                        {
                            "https://api.example.invalid",
                            " https://assets.example.invalid "
                        }
                    }) as IReadOnlyCollection<string>;

                Assert.That(resolved, Is.Not.Null);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "https://api.example.invalid",
                        "https://assets.example.invalid"
                    },
                    resolved);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BootstrapStoresAuthenticationThroughTheSharedSessionContract()
        {
            FieldInfo sessionField = typeof(WebViewerBootstrap).GetField(
                "authenticationSession",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(sessionField, Is.Not.Null);
            Assert.That(
                sessionField.FieldType,
                Is.EqualTo(typeof(IAuthenticationSession)));
        }

        [Test]
        public void RuntimeConnectionRequiresItsAdvertisedTargetAndSession()
        {
            string targetId = "template-runtime-connection-test-" +
                              Guid.NewGuid().ToString("N");
            var registeredSession = AuthenticationSession.CreateTransient();
            IDisposable registration =
                AuthenticationTargetRegistry.Register(
                    targetId,
                    "Template Runtime Connection Test",
                    registeredSession);
            ViewerRuntimeConnection validConnection = null;
            ViewerRuntimeConnection mismatchedConnection = null;
            try
            {
                validConnection = new ViewerRuntimeConnection(
                    targetId,
                    registeredSession,
                    ApiClientFactory.CreateDefault(),
                    "https://api.example.invalid",
                    null,
                    NoopDisposable.Instance);
                mismatchedConnection = new ViewerRuntimeConnection(
                    targetId,
                    AuthenticationSession.CreateTransient(),
                    ApiClientFactory.CreateDefault(),
                    "https://api.example.invalid",
                    null,
                    NoopDisposable.Instance);
                MethodInfo validate = GetPrivateMethod(
                    "IsValidRuntimeConnection");

                Assert.That(
                    validate.Invoke(null, new object[] { validConnection }),
                    Is.True);
                Assert.That(
                    validate.Invoke(null, new object[] { mismatchedConnection }),
                    Is.False);
            }
            finally
            {
                validConnection?.Dispose();
                mismatchedConnection?.Dispose();
                registration.Dispose();
            }
        }

        [Test]
        public void BootstrapConsumesResolvedConnectionWithoutALocalTarget()
        {
            string targetId = "template-resolved-connection-test-" +
                              Guid.NewGuid().ToString("N");
            var provider = new RecordingConnectionProvider(targetId);
            IDisposable providerRegistration =
                ViewerRuntimeConnectionProviderRegistry.Register(provider);
            GameObject root = new GameObject("Resolved Connection Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                MethodInfo compose = GetPrivateMethod(
                    "ComposeAuthentication");
                object[] arguments = { null, null, null };

                compose.Invoke(bootstrap, arguments);

                FieldInfo sessionField = typeof(WebViewerBootstrap).GetField(
                    "authenticationSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo localRegistrationField =
                    typeof(WebViewerBootstrap).GetField(
                        "authenticationTargetRegistration",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(provider.CreateCount, Is.EqualTo(1));
                Assert.That(
                    sessionField?.GetValue(bootstrap),
                    Is.SameAs(provider.Session));
                Assert.That(arguments[0], Is.SameAs(provider.ApiClient));
                Assert.That(
                    arguments[1],
                    Is.EqualTo("https://api.example.invalid"));
                var effectiveOrigins = arguments[2] as
                    IReadOnlyCollection<string>;
                Assert.That(effectiveOrigins, Is.Not.Null);
                Assert.That(
                    effectiveOrigins,
                    Has.Member("https://api.example.invalid"));
                Assert.That(
                    localRegistrationField?.GetValue(bootstrap),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                provider.LastConnection?.Dispose();
                providerRegistration.Dispose();
            }
        }

        [Test]
        public void PartialCompositionCleanupReleasesTheRuntimeConnection()
        {
            string targetId = "template-partial-cleanup-test-" +
                              Guid.NewGuid().ToString("N");
            var provider = new RecordingConnectionProvider(targetId);
            IDisposable providerRegistration =
                ViewerRuntimeConnectionProviderRegistry.Register(provider);
            GameObject root = new GameObject("Partial Composition Cleanup Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                MethodInfo compose = GetPrivateMethod("ComposeAuthentication");
                MethodInfo release = GetPrivateMethod("ReleaseComposition");

                compose.Invoke(bootstrap, new object[] { null, null, null });
                Assert.That(
                    AuthenticationTargetRegistry.TryGet(
                        targetId,
                        out _),
                    Is.True);

                release.Invoke(bootstrap, null);

                Assert.That(
                    AuthenticationTargetRegistry.TryGet(
                        targetId,
                        out _),
                    Is.False);
                ViewerRuntimeConnectionResolution retry =
                    ViewerRuntimeConnectionProviderRegistry.Resolve();
                Assert.That(
                    retry.Status,
                    Is.EqualTo(
                        ViewerRuntimeConnectionResolutionStatus.Resolved));
                retry.Connection.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(root);
                provider.LastConnection?.Dispose();
                providerRegistration.Dispose();
            }
        }

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
                    IInteractiveAuthenticationAcquisitionProvider;

                Assert.That(
                    bootstrap.ResolvedAuthenticationTokenEndpointProfile,
                    Is.SameAs(profile));
                Assert.That(provider, Is.Not.Null);
                Assert.That(provider.DisplayName, Is.EqualTo("Get New Token"));
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

        private static MethodInfo GetPrivateMethod(string name)
        {
            MethodInfo method = typeof(WebViewerBootstrap).GetMethod(
                name,
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private sealed class NoopDisposable : IDisposable
        {
            internal static readonly NoopDisposable Instance =
                new NoopDisposable();

            public void Dispose()
            {
            }
        }

        private sealed class RecordingConnectionProvider :
            IViewerRuntimeConnectionProvider
        {
            private readonly string targetId;

            internal RecordingConnectionProvider(string id)
            {
                targetId = id;
                Session = AuthenticationSession.CreateTransient();
                ApiClient = ApiClientFactory.CreateDefault();
            }

            public string Id => targetId;

            internal AuthenticationSession Session { get; }

            internal IApiClient ApiClient { get; }

            internal ViewerRuntimeConnection LastConnection { get; private set; }

            internal int CreateCount { get; private set; }

            public bool TryCreate(
                out ViewerRuntimeConnection connection,
                out string error)
            {
                CreateCount++;
                IDisposable targetRegistration =
                    AuthenticationTargetRegistry.Register(
                        targetId,
                        "Resolved Connection Test",
                        Session);
                LastConnection = new ViewerRuntimeConnection(
                    targetId,
                    Session,
                    ApiClient,
                    "https://api.example.invalid",
                    null,
                    targetRegistration);
                connection = LastConnection;
                error = null;
                return true;
            }
        }
    }
}
