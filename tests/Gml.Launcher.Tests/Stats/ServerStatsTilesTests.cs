using System.Globalization;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class ServerStatsTilesTests
{
    private static readonly TimeZoneInfo PlusOne =
        TimeZoneInfo.CreateCustomTimeZone("+1", TimeSpan.FromHours(1), "+1", "+1");

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static string Text(string key) => key switch
    {
        SystemConstants.StatsHoursShort => "h",
        SystemConstants.StatsMinutesShort => "min",
        SystemConstants.StatsOutOf => "of",
        SystemConstants.StatsToday => "today",
        SystemConstants.StatsYesterday => "yesterday",
        _ => key
    };

    [Test]
    public void ShowsServerTimeClientTimeLastVisitAndAdvancementsWithProgress()
    {
        var stats = new ServerPlayerStats(TimeSpan.FromSeconds(557862), 145,
            new DateTimeOffset(2026, 11, 17, 19, 1, 56, TimeSpan.Zero), 197, 533);

        var tiles = ServerStatsTiles.Build(stats, new TimeSpan(56, 26, 0), Text, CultureInfo.InvariantCulture,
            PlusOne, Now);

        Assert.That(tiles, Is.EqualTo(new[]
        {
            new StatTile("154 h 57 min", SystemConstants.StatsServerPlayTime, IsAccent: true),
            new StatTile("56 h 26 min", SystemConstants.StatsClientTime),
            new StatTile("11/17, 20:01", SystemConstants.StatsServerLastSeen),
            new StatTile("197 of 533", SystemConstants.StatsServerAdvancements, Progress: 197 / 533.0)
        }));
    }

    [Test]
    public void LastVisitYesterday_IsShownAsYesterdayInLocalTime()
    {
        var stats = new ServerPlayerStats(TimeSpan.FromHours(2), 3,
            new DateTimeOffset(2026, 11, 19, 18, 51, 41, TimeSpan.Zero), 11, 533);

        var tiles = ServerStatsTiles.Build(stats, TimeSpan.Zero, Text, CultureInfo.InvariantCulture, PlusOne, Now);

        Assert.That(tiles[2].Value, Is.EqualTo("yesterday, 19:51"));
    }

    [Test]
    public void WithoutClientTimeLastVisitOrTotal_SkipsWhatIsUnknown()
    {
        var stats = new ServerPlayerStats(TimeSpan.FromMinutes(5), 0, null, 3, 0);

        var tiles = ServerStatsTiles.Build(stats, null, Text, CultureInfo.InvariantCulture, PlusOne, Now);

        Assert.That(tiles, Is.EqualTo(new[]
        {
            new StatTile("5 min", SystemConstants.StatsServerPlayTime, IsAccent: true),
            new StatTile("3", SystemConstants.StatsServerAdvancements)
        }));
    }
}
