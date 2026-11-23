using System;
using System.Collections.Generic;
using System.Linq;

namespace Gml.Launcher.Core.Mods;

/// <summary>One line of a mod's dependency tree: <see cref="Depth" /> 0 is a direct dependency.</summary>
public sealed record DependencyTreeNode(string Key, int Depth);

/// <summary>
///     Which optional mods need which, as set in the panel. Mods are named by their jar file name
///     (e.g. <c>sodium-optional-mod.jar</c>); dependencies on mods the profile does not have are ignored.
/// </summary>
public sealed class OptionalModGraph
{
    private readonly HashSet<string> _available;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _dependencies;

    public OptionalModGraph(IEnumerable<string> available,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependencies)
    {
        _available = new HashSet<string>(available, StringComparer.Ordinal);
        _dependencies = dependencies;
    }

    public static OptionalModGraph Empty { get; } = new([], new Dictionary<string, IReadOnlyList<string>>());

    /// <summary>The mods <paramref name="key" /> lists directly, in the panel's order.</summary>
    public IReadOnlyList<string> DependenciesOf(string key) =>
        _dependencies.TryGetValue(key, out var dependencies)
            ? dependencies.Where(d => _available.Contains(d) && d != key).Distinct().ToArray()
            : [];

    /// <summary>Everything turned on together with <paramref name="key" />, through any number of steps.</summary>
    public IReadOnlyCollection<string> AllDependenciesOf(string key)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(DependenciesOf(key));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == key || !found.Add(current))
                continue;
            foreach (var next in DependenciesOf(current))
                queue.Enqueue(next);
        }

        return found;
    }

    /// <summary>Every mod that must be on because of the enabled ones.</summary>
    public HashSet<string> RequiredBy(IEnumerable<string> enabled)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in enabled)
            required.UnionWith(AllDependenciesOf(key));
        return required;
    }

    /// <summary>The enabled mods (other than <paramref name="key" /> itself) that need it.</summary>
    public IReadOnlyList<string> NeededBy(string key, IEnumerable<string> enabled) =>
        enabled.Where(other => other != key && AllDependenciesOf(other).Contains(key)).ToArray();

    /// <summary>
    ///     The dependency tree of <paramref name="key" />, depth first. Each mod appears once (where it is first
    ///     reached), so a shared dependency or a loop does not repeat.
    /// </summary>
    public IReadOnlyList<DependencyTreeNode> Tree(string key)
    {
        var nodes = new List<DependencyTreeNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { key };
        Walk(key, 0);
        return nodes;

        void Walk(string parent, int depth)
        {
            foreach (var child in DependenciesOf(parent))
            {
                if (!seen.Add(child))
                    continue;
                nodes.Add(new DependencyTreeNode(child, depth));
                Walk(child, depth + 1);
            }
        }
    }

    /// <summary>
    ///     The mods that get their own card: those no other mod needs. A dependency is shown inside the card of
    ///     the mod that needs it. Mods that only need each other (a loop) still get one card. Keeps the given order.
    /// </summary>
    public IReadOnlyList<string> TopLevel(IReadOnlyList<string> keys)
    {
        var needed = new HashSet<string>(keys.SelectMany(DependenciesOf), StringComparer.Ordinal);
        var top = new HashSet<string>(keys.Where(key => !needed.Contains(key)), StringComparer.Ordinal);
        var shown = new HashSet<string>(top, StringComparer.Ordinal);
        foreach (var key in top)
            shown.UnionWith(AllDependenciesOf(key));

        foreach (var key in keys.Where(key => !shown.Contains(key)))
        {
            top.Add(key);
            shown.Add(key);
            shown.UnionWith(AllDependenciesOf(key));
        }

        return keys.Where(top.Contains).ToArray();
    }
}
