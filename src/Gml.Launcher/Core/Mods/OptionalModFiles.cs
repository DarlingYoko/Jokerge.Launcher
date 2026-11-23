using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gml.Launcher.Core.Mods;

/// <summary>
///     Optional mods in a profile's mods folder: <c>name-optional-mod.jar</c> is on,
///     <c>name-optional-mod.jar.disabled</c> is off. The key is the enabled file name.
/// </summary>
public sealed class OptionalModFiles(string modsDirectory)
{
    private const string Suffix = "-optional-mod.jar";
    private const string Disabled = ".disabled";

    public static string DirectoryFor(string installationDirectory, string profileName) =>
        Path.Combine(installationDirectory, "game data", profileName, "mods");

    public IReadOnlyCollection<string> EnabledKeys() => Keys("*" + Suffix);

    /// <summary>Optional mods present on disk, on or off.</summary>
    public IReadOnlyCollection<string> AllKeys() =>
        EnabledKeys().Concat(Keys("*" + Suffix + Disabled).Select(k => k[..^Disabled.Length])).Distinct().ToArray();

    /// <summary>Whether the mod's file is on disk and not an empty placeholder.</summary>
    public bool IsPresent(string key) => NonEmpty(EnabledPath(key)) || NonEmpty(DisabledPath(key));

    /// <summary>Renames the files; returns the keys that could not be turned on because their file is missing.</summary>
    public IReadOnlyList<string> Apply(OptionalModChange change)
    {
        var missing = new List<string>();

        foreach (var key in change.ToDisable)
        {
            if (File.Exists(EnabledPath(key)))
                File.Move(EnabledPath(key), DisabledPath(key), overwrite: true);
        }

        foreach (var key in change.ToEnable)
        {
            if (NonEmpty(EnabledPath(key)))
                continue;
            if (NonEmpty(DisabledPath(key)))
                File.Move(DisabledPath(key), EnabledPath(key), overwrite: true);
            else
                missing.Add(key);
        }

        return missing;
    }

    private string EnabledPath(string key) => Path.Combine(modsDirectory, key);

    private string DisabledPath(string key) => Path.Combine(modsDirectory, key + Disabled);

    private static bool NonEmpty(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private IReadOnlyCollection<string> Keys(string pattern) =>
        Directory.Exists(modsDirectory)
            ? Directory.GetFiles(modsDirectory, pattern).Select(Path.GetFileName).OfType<string>()
                // "*.jar" also matches "*.jar.disabled" on Windows (8.3 names), so filter exactly.
                .Where(name => name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];
}
