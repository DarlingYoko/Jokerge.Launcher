using System;
using System.IO;
using System.Linq;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Finds a Java runtime the launcher has downloaded into the installation folder.</summary>
public static class JavaLocator
{
    public static (string? Path, string? Version) Find(string installationDirectory, bool isWindows)
    {
        if (!Directory.Exists(installationDirectory)) return (null, null);

        var executable = isWindows ? "java.exe" : "java";
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 8,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            var java = Directory.EnumerateFiles(installationDirectory, executable, options)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "bin",
                    StringComparison.OrdinalIgnoreCase));

            if (java is null) return (null, null);

            var javaHome = Path.GetDirectoryName(Path.GetDirectoryName(java))!;
            var releaseFile = Path.Combine(javaHome, "release");
            var version = File.Exists(releaseFile) ? JavaReleaseParser.Describe(File.ReadAllText(releaseFile)) : null;

            return (java, version);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }
}
