using Gml.Client.Interfaces;
using Gml.Launcher.Core.Routes;
using Gml.Launcher.Core.Services;

namespace Gml.Launcher.Tests.Routes;

internal static class SampleRoutes
{
    public static ServerRouteDto Direct() => new() { Name = "Напрямую", Address = "waffle.jokerge.com", Port = 25565 };
    public static ServerRouteDto Moscow() => new() { Name = "Москва", Address = "msk.jokerge.com", Port = 25565 };
    public static ServerRouteDto Frankfurt() => new() { Name = "Франкфурт", Address = "fra.jokerge.com", Port = 25565 };
}

/// <summary>Routes per profile; a profile that is not listed fails to load (as when the API is down).</summary>
internal sealed class FakeRoutesSource : IServerRoutesSource
{
    public Dictionary<string, IReadOnlyList<ServerRouteDto>> Routes { get; } = new();
    public List<string> Requests { get; } = [];

    public Task<IReadOnlyList<ServerRouteDto>?> GetRoutesAsync(string profileName,
        CancellationToken token = default)
    {
        Requests.Add(profileName);
        return Task.FromResult(Routes.TryGetValue(profileName, out var routes) ? routes : null);
    }
}

/// <summary>Answers by host; unknown hosts are offline.</summary>
internal sealed class FakePinger : IMinecraftStatusPinger
{
    public Dictionary<string, ServerPingResult> Results { get; } = new();
    public List<string> Pinged { get; } = [];

    public Task<ServerPingResult> PingAsync(string host, int port, CancellationToken token = default)
    {
        lock (Pinged)
            Pinged.Add(host);
        return Task.FromResult(Results.GetValueOrDefault(host, ServerPingResult.Offline));
    }
}

internal sealed class InMemoryStorage : IStorageService
{
    public Dictionary<string, object?> Values { get; } = new();

    public Task SetAsync<T>(string key, T value, CancellationToken? token = default)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key) =>
        Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)value : default);

    public Task<int> SaveRecord<T>(T value) => Task.FromResult(0);

    public Task<string> GetLogsAsync(int rowCount) => Task.FromResult(string.Empty);
}

internal sealed class KeysAsText : ILocalizationService
{
    public string GetString(string key) => key;
}
