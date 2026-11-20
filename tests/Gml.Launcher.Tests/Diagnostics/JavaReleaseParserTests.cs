using Gml.Launcher.Core.Diagnostics;

namespace Gml.Launcher.Tests.Diagnostics;

public class JavaReleaseParserTests
{
    [Test]
    public void ReadsJavaVersionFromReleaseFile()
    {
        const string release = "IMPLEMENTOR=\"Eclipse Adoptium\"\nJAVA_VERSION=\"21.0.3\"\nOS_NAME=\"Windows\"";

        Assert.That(JavaReleaseParser.ParseVersion(release), Is.EqualTo("21.0.3"));
    }

    [Test]
    public void AddsImplementorWhenPresent()
    {
        const string release = "JAVA_VERSION=\"17.0.9\"\r\nIMPLEMENTOR=\"Azul Systems, Inc.\"";

        Assert.That(JavaReleaseParser.Describe(release), Is.EqualTo("17.0.9 (Azul Systems, Inc.)"));
    }

    [TestCase("")]
    [TestCase("OS_NAME=\"Linux\"")]
    public void MissingVersion_IsNull(string release)
    {
        Assert.That(JavaReleaseParser.ParseVersion(release), Is.Null);
    }
}
