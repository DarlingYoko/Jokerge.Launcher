using System.Globalization;

namespace Gml.Launcher.Core.Diagnostics;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Binary units, invariant culture: 1536 → "1.5 KB".</summary>
    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            unit++;
            value /= 1024;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", value, Units[unit]);
    }
}
