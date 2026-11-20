using System;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Reads the <c>release</c> file every JDK/JRE ships next to its <c>bin</c> folder.</summary>
public static partial class JavaReleaseParser
{
    [GeneratedRegex(@"(?m)^\s*JAVA_VERSION\s*=\s*""?([^""\r\n]+)""?")]
    private static partial Regex Version();

    [GeneratedRegex(@"(?m)^\s*IMPLEMENTOR\s*=\s*""?([^""\r\n]+)""?")]
    private static partial Regex Implementor();

    public static string? ParseVersion(string? release)
    {
        if (string.IsNullOrEmpty(release)) return null;
        var match = Version().Match(release);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>"21.0.3 (Eclipse Adoptium)", or just the version, or null.</summary>
    public static string? Describe(string? release)
    {
        var version = ParseVersion(release);
        if (version is null) return null;

        var implementor = Implementor().Match(release!);
        return implementor.Success && !string.IsNullOrWhiteSpace(implementor.Groups[1].Value)
            ? $"{version} ({implementor.Groups[1].Value.Trim()})"
            : version;
    }
}
