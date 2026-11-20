using Gml.Launcher.Core.Diagnostics;

namespace Gml.Launcher.Tests.Diagnostics;

/// <summary>Same masking rules as the API's DiagnosticTextRedactor (and its SentryMessageNormalizer).</summary>
public class DiagnosticRedactorTests
{
    [TestCase(@"C:\Users\Artem Zaitsev\AppData\Roaming\jokerge", @"C:\Users\<user>\AppData\Roaming\jokerge")]
    [TestCase("C:/Users/yoko/AppData", "C:/Users/<user>/AppData")]
    [TestCase("/home/yoko/.jokerge", "/home/<user>/.jokerge")]
    [TestCase("/Users/yoko/Library", "/Users/<user>/Library")]
    public void ProfilePaths_HideAccountName(string input, string expected)
    {
        Assert.That(DiagnosticRedactor.Redact(input), Is.EqualTo(expected));
    }

    [TestCase("x eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ5b2tvIn0.c2lnbmF0dXJl y", "x <token> y")]
    [TestCase("Authorization: Bearer abc.DEF-123_x", "Authorization: Bearer <token>")]
    [TestCase("password=hunter2", "password=<hidden>")]
    [TestCase("\"accessToken\":\"abc123\"", "\"accessToken\":\"<hidden>\"")]
    [TestCase("--accessToken 0123456789abcdef --version 1.20", "--accessToken <hidden> --version 1.20")]
    public void Secrets_AreMasked(string input, string expected)
    {
        Assert.That(DiagnosticRedactor.Redact(input), Is.EqualTo(expected));
    }

    [Test]
    public void KnownSecrets_AreMasked_ShortOnesIgnored()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DiagnosticRedactor.Redact("token-value-xyz in log", ["token-value-xyz"]),
                Is.EqualTo("<hidden> in log"));
            Assert.That(DiagnosticRedactor.Redact("a b c", ["", null, "a"]), Is.EqualTo("a b c"));
        });
    }

    [Test]
    public void WindowsAccountName_OutsideProfilePath_IsMaskedAsWholeWord()
    {
        var text = DiagnosticRedactor.Redact(@"D:\yoko\games and yokozuna", accountNames: ["yoko"]);

        Assert.That(text, Is.EqualTo(@"D:\<user>\games and yokozuna"));
    }

    [Test]
    public void OrdinaryText_IsUntouched()
    {
        const string text = "API https://launcher.jokerge.com answered 200 in 42 ms";

        Assert.That(DiagnosticRedactor.Redact(text), Is.EqualTo(text));
    }
}
