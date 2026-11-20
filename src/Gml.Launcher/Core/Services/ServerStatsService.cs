using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gml.Launcher.Core.Stats;
using Sentry;

namespace Gml.Launcher.Core.Services;

/// <summary>
///     Downloads <c>players.json</c> of a profile's stats site (address from <see cref="ServerStatsUrl" />). The site
///     rebuilds it once an hour, so a downloaded file is reused for <see cref="CacheFor" />; a failed download is
///     not, and the block is simply not shown.
/// </summary>
public class ServerStatsService(HttpClient http, string urlTemplate, TimeProvider? clock = null)
    : IServerStatsService
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<Uri, (DateTimeOffset At, string Json)> _cache = new();
    private readonly object _lock = new();

    public static HttpClient CreateHttpClient() => new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<ServerPlayerStats?> GetAsync(string profileName, string playerUuid, string playerName,
        CancellationToken token = default)
    {
        var url = ServerStatsUrl.For(urlTemplate, profileName);
        if (url is null)
            return null;

        var json = await DownloadAsync(url, token).ConfigureAwait(false);
        if (json is null)
            return null;

        try
        {
            return ServerStatsParser.Find(json, playerUuid, playerName);
        }
        catch (JsonException exception)
        {
            SentrySdk.CaptureException(exception);
            lock (_lock)
                _cache.Remove(url);
            return null;
        }
    }

    private async Task<string?> DownloadAsync(Uri url, CancellationToken token)
    {
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            if (_cache.TryGetValue(url, out var cached) && now - cached.At < CacheFor)
                return cached.Json;
        }

        try
        {
            using var response = await http.GetAsync(url, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            lock (_lock)
                _cache[url] = (now, json);
            return json;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException &&
                                          !token.IsCancellationRequested)
        {
            // No stats site for this server, or it is down: nothing to show, nothing to report.
            return null;
        }
    }
}
