using Deucarian.API.Models;
using Deucarian.TemplateViewerWeb.Loading;
using NUnit.Framework;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerModelAuthenticationPolicyTests
    {
        [Test]
        public void RelativeModelUsesLiveProviderWithoutTokenOverride()
        {
            Assert.That(
                WebViewerModelAuthenticationPolicy.Resolve(
                    "models/current.bundle",
                    "https://api.example.com/v1"),
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void SameOriginAbsoluteModelUsesLiveProvider()
        {
            Assert.That(
                WebViewerModelAuthenticationPolicy.Resolve(
                    "https://api.example.com/models/current.bundle",
                    "https://api.example.com/v1"),
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void UntrustedCrossOriginModelNeverReceivesSessionToken()
        {
            Assert.That(
                WebViewerModelAuthenticationPolicy.Resolve(
                    "https://cdn.other.example/model.bundle",
                    "https://api.example.com/v1"),
                Is.EqualTo(ApiAuthenticationRequirement.Disabled));
        }

        [Test]
        public void ExplicitExactOriginAllowsPrivateCdn()
        {
            Assert.That(
                WebViewerModelAuthenticationPolicy.Resolve(
                    "https://cdn.example.com/model.bundle",
                    "https://api.example.com/v1",
                    new[] { "https://cdn.example.com" }),
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void OriginEntryWithPathIsNotTreatedAsAnAllowlistOrigin()
        {
            Assert.That(
                WebViewerModelAuthenticationPolicy.Resolve(
                    "https://cdn.example.com/model.bundle",
                    "https://api.example.com/v1",
                    new[] { "https://cdn.example.com/private" }),
                Is.EqualTo(ApiAuthenticationRequirement.Disabled));
        }
    }
}
