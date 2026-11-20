using System.Collections.Generic;

namespace Gml.Launcher.Core.Stats;

/// <summary>Remembers what an immutable log archive contained, keyed by name, size and write time.</summary>
public interface ILogSpanCache
{
    /// <returns><c>true</c> on a hit; <paramref name="span"/> is <c>null</c> for a log without timestamps.</returns>
    bool TryGet(string key, out LogSpan? span);

    void Set(string key, LogSpan? span);
}

/// <summary>Cache that lives as long as the object; <see cref="Entries" /> is what gets persisted.</summary>
public sealed class InMemoryLogSpanCache : ILogSpanCache
{
    public InMemoryLogSpanCache(Dictionary<string, CachedLogSpan>? entries = null)
    {
        Entries = entries ?? new Dictionary<string, CachedLogSpan>();
    }

    public Dictionary<string, CachedLogSpan> Entries { get; }

    public int Count => Entries.Count;

    public bool IsChanged { get; private set; }

    public bool TryGet(string key, out LogSpan? span)
    {
        if (Entries.TryGetValue(key, out var entry))
        {
            span = entry.HasSpan ? new LogSpan(entry.FirstTimeOfDay, entry.Duration) : null;
            return true;
        }

        span = null;
        return false;
    }

    public void Set(string key, LogSpan? span)
    {
        Entries[key] = span is { } value
            ? new CachedLogSpan(true, value.FirstTimeOfDay, value.Duration)
            : new CachedLogSpan(false, default, default);
        IsChanged = true;
    }
}

/// <summary>Serializable form of a cached <see cref="LogSpan" />.</summary>
public sealed record CachedLogSpan(bool HasSpan, System.TimeSpan FirstTimeOfDay, System.TimeSpan Duration);
