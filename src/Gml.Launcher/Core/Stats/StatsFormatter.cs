using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.Stats;

public static partial class StatsFormatter
{
    /// <summary>"12 h 5 min" style; seconds are dropped, days stay hours.</summary>
    public static string Duration(TimeSpan value, string hoursUnit, string minutesUnit)
    {
        var hours = (long)value.TotalHours;
        var minutes = value.Minutes;

        if (hours == 0)
            return $"{minutes.ToString(CultureInfo.InvariantCulture)} {minutesUnit}";

        var text = $"{hours.ToString(CultureInfo.InvariantCulture)} {hoursUnit}";
        return minutes == 0 ? text : $"{text} {minutes.ToString(CultureInfo.InvariantCulture)} {minutesUnit}";
    }

    /// <summary>
    ///     Short point in time for a stat tile: "today, 17:07", "yesterday, 19:51", "19.11, 09:05" this year,
    ///     the culture's full short date and time otherwise.
    /// </summary>
    public static string Moment(DateTime value, DateTime now, CultureInfo culture, string today, string yesterday)
    {
        var format = culture.DateTimeFormat;
        var time = value.ToString(format.ShortTimePattern, culture);

        if (value.Date == now.Date)
            return $"{today}, {time}";
        if (value.Date == now.Date.AddDays(-1))
            return $"{yesterday}, {time}";
        if (value.Year == now.Year)
            return $"{value.ToString(YearPart().Replace(format.ShortDatePattern, string.Empty), culture)}, {time}";

        return value.ToString("g", culture);
    }

    // The year and the separator next to it: "dd.MM.yyyy" -> "dd.MM", "M/d/yyyy" -> "M/d", "yyyy/M/d" -> "M/d".
    [GeneratedRegex("[^dM]*y+[^dM]*")]
    private static partial Regex YearPart();
}
