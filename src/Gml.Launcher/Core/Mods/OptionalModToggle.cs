using System;
using System.Collections.Generic;
using System.Linq;

namespace Gml.Launcher.Core.Mods;

/// <summary>
///     What to rename in the mods folder, and which enabled mods are on only because something needs them
///     (<see cref="Automatic" />): those go off again when nothing needs them any more.
/// </summary>
public sealed record OptionalModChange(
    IReadOnlyCollection<string> ToEnable,
    IReadOnlyCollection<string> ToDisable,
    IReadOnlyCollection<string> Automatic)
{
    public bool IsEmpty => ToEnable.Count == 0 && ToDisable.Count == 0;
}

/// <summary>Plans turning optional mods on and off so that every enabled mod has its dependencies on.</summary>
public static class OptionalModToggle
{
    /// <summary>The player turns <paramref name="key" /> on: its whole dependency chain comes with it.</summary>
    public static OptionalModChange Enable(OptionalModGraph graph, IReadOnlyCollection<string> enabled,
        IReadOnlyCollection<string> automatic, string key)
    {
        var enabledSet = Set(enabled);
        var toEnable = graph.AllDependenciesOf(key).Prepend(key).Where(k => !enabledSet.Contains(k)).Distinct()
            .ToArray();

        var newAutomatic = Set(automatic);
        newAutomatic.UnionWith(toEnable);
        newAutomatic.Remove(key); // the player's own choice from now on

        enabledSet.UnionWith(toEnable);
        newAutomatic.IntersectWith(enabledSet);

        return new OptionalModChange(toEnable, [], newAutomatic);
    }

    /// <summary>
    ///     The player turns <paramref name="key" /> off. Refused (<c>null</c>) while an enabled mod needs it;
    ///     dependencies that came on automatically go off with it unless something else still needs them.
    /// </summary>
    public static OptionalModChange? Disable(OptionalModGraph graph, IReadOnlyCollection<string> enabled,
        IReadOnlyCollection<string> automatic, string key)
    {
        if (graph.NeededBy(key, enabled).Count > 0)
            return null;

        var remaining = Set(enabled);
        remaining.Remove(key);
        var newAutomatic = Set(automatic);
        newAutomatic.Remove(key);
        var toDisable = new List<string> { key };

        // Turning one automatic mod off can free the ones only it needed, so repeat until nothing changes.
        while (true)
        {
            var required = graph.RequiredBy(remaining);
            var freed = newAutomatic.Where(k => remaining.Contains(k) && !required.Contains(k)).ToArray();
            if (freed.Length == 0)
                break;

            foreach (var mod in freed)
            {
                remaining.Remove(mod);
                newAutomatic.Remove(mod);
                toDisable.Add(mod);
            }
        }

        newAutomatic.IntersectWith(remaining);
        return new OptionalModChange([], toDisable, newAutomatic);
    }

    /// <summary>Turns on whatever the enabled mods need but is off (a dependency added later, a hand-renamed file).</summary>
    public static OptionalModChange Repair(OptionalModGraph graph, IReadOnlyCollection<string> enabled,
        IReadOnlyCollection<string> automatic)
    {
        var enabledSet = Set(enabled);
        var toEnable = graph.RequiredBy(enabled).Where(k => !enabledSet.Contains(k)).ToArray();

        var newAutomatic = Set(automatic);
        newAutomatic.UnionWith(toEnable);
        enabledSet.UnionWith(toEnable);
        newAutomatic.IntersectWith(enabledSet);

        return new OptionalModChange(toEnable, [], newAutomatic);
    }

    private static HashSet<string> Set(IEnumerable<string> keys) => new(keys, StringComparer.Ordinal);
}
