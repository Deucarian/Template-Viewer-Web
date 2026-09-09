using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerOptionalDependencyGuardTests
    {
        [TestCase("Runtime/SimultriaIntegration/Deucarian.TemplateViewerWeb.SimultriaIntegration.asmdef")]
        [TestCase("Tests/SimultriaIntegration/Deucarian.TemplateViewerWeb.SimultriaIntegration.Tests.asmdef")]
        [TestCase("Tests/SimultriaIntegrationPlayMode/Deucarian.TemplateViewerWeb.SimultriaIntegration.Tests.PlayMode.asmdef")]
        public void StartupBridgeAndItsTestsRequireBothOptionalPackageGuards(string path)
        {
            JObject assembly = ReadPackageJson(path);
            AssertGuard(assembly, "com.deucarian.simultria-viewer-integration",
                "1.2.1", "DEUCARIAN_SIMULTRIA_STARTUP_STATUS");
            AssertGuard(assembly, "com.deucarian.api",
                "2.0.2", "DEUCARIAN_SIMULTRIA_STARTUP_API");
        }

        [Test]
        public void OptionalStartupPackagesDoNotBecomeHardAdapterDependencies()
        {
            JObject manifest = ReadPackageJson("package.json");
            JObject governance = ReadPackageJson("deucarian-package.json");
            string[] optional = governance["optionalVersionDefinedDependencies"]
                .Values<string>().ToArray();
            string[] required = governance["requiredDependencies"]
                .Values<string>().ToArray();
            foreach (string package in new[]
                     { "com.deucarian.api", "com.deucarian.simultria-viewer-integration" })
            {
                Assert.That(manifest["dependencies"][package], Is.Null, package);
                Assert.That(optional, Does.Contain(package));
                Assert.That(required, Does.Not.Contain(package));
            }
        }

        private static void AssertGuard(
            JObject assembly, string package, string minimum, string symbol)
        {
            JToken[] matches = assembly["versionDefines"].Children()
                .Where(value => value.Value<string>("name") == package).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), package);
            Assert.That(matches[0].Value<string>("expression"), Is.EqualTo(minimum));
            Assert.That(matches[0].Value<string>("define"), Is.EqualTo(symbol));
            Assert.That(assembly["defineConstraints"].Values<string>(),
                Does.Contain(symbol), "The version define must actually gate the assembly.");
        }

        private static JObject ReadPackageJson(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(WebViewerBootstrap).Assembly);
            return JObject.Parse(File.ReadAllText(Path.Combine(package.resolvedPath, relativePath)));
        }
    }
}
