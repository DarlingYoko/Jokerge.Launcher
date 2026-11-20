using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gml.Launcher.Core.Stats;

/// <summary>One number on a profile's stats card.</summary>
/// <param name="IsAccent">The headline number, drawn in the primary colour.</param>
/// <param name="Progress">0..1 when the value has a known maximum (advancements of all there are).</param>
public sealed record StatTile(string Value, string Label, bool IsAccent = false, double? Progress = null)
{
    public bool HasProgress => Progress is not null;

    /// <summary><see cref="Progress" /> for a progress bar, which needs a plain number.</summary>
    public double ProgressValue => Progress ?? 0;
}

/// <summary>Turns <see cref="ServerPlayerStats" /> into the tiles of a profile's stats card.</summary>
public static class ServerStatsTiles
{
    /// <param name="clientTime">How long the profile's client ran on this computer; <c>null</c> when unknown.</param>
    /// <param name="text">Localized string for a <see cref="SystemConstants" /> key.</param>
    /// <param name="zone">Time zone the last visit is shown in.</param>
    public static IReadOnlyList<StatTile> Build(ServerPlayerStats stats, TimeSpan? clientTime,
        Func<string, string> text, CultureInfo culture, TimeZoneInfo zone, DateTimeOffset now)
    {
        string Duration(TimeSpan value) => StatsFormatter.Duration(value, text(SystemConstants.StatsHoursShort),
            text(SystemConstants.StatsMinutesShort));

        var tiles = new List<StatTile>
        {
            new(Duration(stats.PlayTime), text(SystemConstants.StatsServerPlayTime), IsAccent: true)
        };

        if (clientTime is { } client)
            tiles.Add(new StatTile(Duration(client), text(SystemConstants.StatsClientTime)));

        if (stats.LastSeen is { } lastSeen)
            tiles.Add(new StatTile(
                StatsFormatter.Moment(TimeZoneInfo.ConvertTime(lastSeen, zone).DateTime,
                    TimeZoneInfo.ConvertTime(now, zone).DateTime, culture,
                    text(SystemConstants.StatsToday), text(SystemConstants.StatsYesterday)),
                text(SystemConstants.StatsServerLastSeen)));

        var advancements = stats.Advancements.ToString(culture);
        tiles.Add(stats.AdvancementTotal > 0
            ? new StatTile(
                $"{advancements} {text(SystemConstants.StatsOutOf)} {stats.AdvancementTotal.ToString(culture)}",
                text(SystemConstants.StatsServerAdvancements),
                Progress: Math.Clamp(stats.Advancements / (double)stats.AdvancementTotal, 0, 1))
            : new StatTile(advancements, text(SystemConstants.StatsServerAdvancements)));

        return tiles;
    }
}
