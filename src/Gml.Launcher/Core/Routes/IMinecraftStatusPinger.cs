using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Routes;

/// <summary>Checks a Minecraft server from this computer with the Server List Ping.</summary>
public interface IMinecraftStatusPinger
{
    /// <summary>Never throws for network failures: an unreachable server is <see cref="ServerPingResult.Offline" />.</summary>
    Task<ServerPingResult> PingAsync(string host, int port, CancellationToken token = default);
}

public static class MinecraftStatusPingerExtensions
{
    /// <summary>Pings all routes at once, so the whole check takes about one ping timeout at most.</summary>
    /// <returns>The results in the order of <paramref name="routes" />.</returns>
    public static Task<ServerPingResult[]> PingAllAsync(this IMinecraftStatusPinger pinger,
        IReadOnlyList<ServerRouteDto> routes, CancellationToken token = default) =>
        Task.WhenAll(routes.Select(route => pinger.PingAsync(route.Address, route.Port, token)));
}
