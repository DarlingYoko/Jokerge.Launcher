using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class ServerStatsUrlTests
{
    private const string Template = "https://{profile}.stats.jokerge.com/players.json";

    [Test]
    public void ProfileNameGoesIntoTheTemplateInLowerCase()
    {
        Assert.That(ServerStatsUrl.For(Template, "Waffle"),
            Is.EqualTo(new Uri("https://waffle.stats.jokerge.com/players.json")));
    }

    [Test]
    public void TemplateWithoutPlaceholder_IsUsedAsIs()
    {
        Assert.That(ServerStatsUrl.For("https://stats.example.org/players.json", "Waffle"),
            Is.EqualTo(new Uri("https://stats.example.org/players.json")));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a url {profile}")]
    [TestCase("ftp://{profile}.example.org/players.json")]
    public void EmptyOrInvalidTemplate_TurnsTheBlockOff(string template)
    {
        Assert.That(ServerStatsUrl.For(template, "Waffle"), Is.Null);
    }

    [TestCase("Waffle Season 2")]
    [TestCase("waffle_2")]
    [TestCase("../admin")]
    [TestCase("")]
    public void ProfileNameThatIsNotAHostLabel_HasNoUrl(string profileName)
    {
        Assert.That(ServerStatsUrl.For(Template, profileName), Is.Null);
    }
}
