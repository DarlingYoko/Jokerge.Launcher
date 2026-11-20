using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Core.Services;

/// <summary>
///     Sums the client logs in <c>&lt;install&gt;/game data/&lt;profile&gt;/logs</c>: one log per start of the
///     client. Spans of archived logs are kept in the launcher storage so a page open does not unpack every
///     archive again.
/// </summary>
public class ClientTimeService(IGmlClientManager manager, IStorageService storage) : IClientTimeService
{
    private const string CacheKeyPrefix = "ProfileStats.LogSpans.";

    public async Task<TimeSpan> GetTotalAsync(string profileName, CancellationToken token = default)
    {
        var logs = Path.Combine(manager.InstallationDirectory, "game data", profileName, "logs");
        var cacheKey = CacheKeyPrefix + profileName;
        var cache = new InMemoryLogSpanCache(
            await storage.GetAsync<Dictionary<string, CachedLogSpan>>(cacheKey).ConfigureAwait(false));

        var total = await Task.Run(() => new ClientLogScanner(cache).TotalTime(logs), token).ConfigureAwait(false);

        if (cache.IsChanged)
            await storage.SetAsync(cacheKey, cache.Entries, token).ConfigureAwait(false);

        return total;
    }
}
