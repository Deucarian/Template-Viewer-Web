using System.Reflection;
using Deucarian.CommandRouting;
using NUnit.Framework;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerLocalCommandIngressTests
    {
        [Test]
        public void BootstrapExposesPackageNeutralSceneCommandPort()
        {
            PropertyInfo property = typeof(WebViewerBootstrap).GetProperty(
                nameof(WebViewerBootstrap.LocalCommandPort));
            FieldInfo field = typeof(WebViewerBootstrap).GetField(
                "localCommandPort",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(CommandRoutePortBehaviour)));
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(CommandRoutePortBehaviour)));
        }
    }
}
