using Gml.Launcher.Core;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Core.Stats;
using Gml.Launcher.ViewModels.Components;

namespace Gml.Launcher.Tests.Stats;

public class ProfileStatsCardViewModelTests
{
    private sealed class KeysAsText : ILocalizationService
    {
        public string GetString(string key) => key;
    }

    private static ProfileStatsCardViewModel Card() => new("Waffle", "Waffle", null, "1.20.1", new KeysAsText());

    [Test]
    public void StartsLoading()
    {
        var card = Card();

        Assert.That(card.IsLoading, Is.True);
        Assert.That(card.Tiles, Is.Empty);
    }

    [Test]
    public void NoStats_LeavesTheCardEmpty()
    {
        var card = Card();

        var shown = card.Apply(null, TimeSpan.FromHours(1));

        Assert.That(shown, Is.False);
        Assert.That(card.IsLoading, Is.False);
        Assert.That(card.Tiles, Is.Empty);
    }

    [Test]
    public void Stats_FillTheTiles()
    {
        var card = Card();

        var shown = card.Apply(new ServerPlayerStats(TimeSpan.FromHours(2), 3, null, 5, 10), TimeSpan.FromHours(7));

        Assert.That(shown, Is.True);
        Assert.That(card.IsLoading, Is.False);
        Assert.That(card.Tiles.Select(t => t.Label), Is.EqualTo(new[]
        {
            SystemConstants.StatsServerPlayTime, SystemConstants.StatsClientTime,
            SystemConstants.StatsServerAdvancements
        }));
    }
}
