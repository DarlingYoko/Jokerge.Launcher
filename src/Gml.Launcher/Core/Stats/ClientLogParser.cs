using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.Stats;

/// <summary>One run of the game client, as recorded by one log file.</summary>
public sealed record GameRun(DateTime Start, TimeSpan Duration);

/// <summary>Time of the first log line and how long the log kept being written.</summary>
public readonly record struct LogSpan(TimeSpan FirstTimeOfDay, TimeSpan Duration)
{
    /// <summary>
    ///     An archived log (<c>yyyy-MM-dd-N.log.gz</c>): the date comes from the file name, the time from its
    ///     first line.
    /// </summary>
    public GameRun ToArchivedRun(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue) + FirstTimeOfDay, Duration);

    /// <summary>
    ///     <c>latest.log</c> has no date anywhere, and on Windows its creation time can be inherited from the file it
    ///     replaced. Its last write is reliable, so the run is placed to end there.
    /// </summary>
    public GameRun ToRunEndingAt(DateTime lastWrite) => new(lastWrite - Duration, Duration);
}

/// <summary>Reads how long a Minecraft client ran from its log file.</summary>
public static partial class ClientLogParser
{
    /// <summary>
    ///     Async appenders can print a line a few seconds "in the past". A step back shorter than this is jitter, a
    ///     longer one means the clock passed midnight.
    /// </summary>
    public static readonly TimeSpan JitterTolerance = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    // Vanilla/Fabric: "[15:31:07] ...", Forge/NeoForge: "[20Nov2026 12:00:01.123] ...".
    [GeneratedRegex(@"^\[(?:\d{2}[A-Za-z]{3}\d{4} )?(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,3})?\]")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})-\d+\.log\.gz$")]
    private static partial Regex ArchiveNameRegex();

    public static LogSpan? ReadSpan(IEnumerable<string> lines)
    {
        TimeSpan? first = null;
        var latest = TimeSpan.Zero;
        var dayOffset = TimeSpan.Zero;

        foreach (var line in lines)
        {
            if (!TryReadTimeOfDay(line, out var timeOfDay))
                continue;

            if (first is null)
            {
                first = timeOfDay;
                latest = timeOfDay;
                continue;
            }

            var absolute = dayOffset + timeOfDay;
            if (absolute < latest - JitterTolerance)
            {
                dayOffset += OneDay;
                absolute += OneDay;
            }

            if (absolute > latest)
                latest = absolute;
        }

        return first is null ? null : new LogSpan(first.Value, latest - first.Value);
    }

    public static bool TryParseArchiveName(string fileName, out DateOnly date)
    {
        date = default;
        var match = ArchiveNameRegex().Match(fileName);

        return match.Success && DateOnly.TryParseExact(
            $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}",
            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryReadTimeOfDay(string line, out TimeSpan timeOfDay)
    {
        timeOfDay = default;
        if (line.Length < 10 || line[0] != '[')
            return false;

        var match = TimestampRegex().Match(line);
        if (!match.Success)
            return false;

        var hours = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture);
        if (hours > 23 || minutes > 59 || seconds > 59)
            return false;

        timeOfDay = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
