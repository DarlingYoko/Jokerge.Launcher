using Gml.Launcher.Core;
using Sentry;

namespace Gml.Launcher.Tests;

/// <summary>
///     The panel's "Errors" tab showed only the Windows account and PC name; the launcher now tags
///     reports with the signed-in Minecraft nickname so the admin sees which player it was.
/// </summary>
public class SentryPlayerTests
{
    private static Scope NewScope() => new(new SentryOptions());

    [Test]
    public void SignedIn_TagsNickname()
    {
        var scope = NewScope();

        SentryPlayer.Apply(scope, " Waffle ");

        Assert.That(scope.Tags[SentryPlayer.TagName], Is.EqualTo("Waffle"));
    }

    [Test]
    public void SignedIn_LeavesWindowsUserAlone()
    {
        var scope = NewScope();
        scope.User = new SentryUser { Username = "yoko" };

        SentryPlayer.Apply(scope, "Waffle");

        Assert.That(scope.User.Username, Is.EqualTo("yoko"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void SignedOut_RemovesTag(string? player)
    {
        var scope = NewScope();
        SentryPlayer.Apply(scope, "Waffle");

        SentryPlayer.Apply(scope, player);

        Assert.That(scope.Tags.ContainsKey(SentryPlayer.TagName), Is.False);
    }
}
