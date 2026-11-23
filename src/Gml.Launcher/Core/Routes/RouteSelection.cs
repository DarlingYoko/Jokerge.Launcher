using System;
using System.Collections.Generic;
using System.Linq;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Routes;

/// <summary>Which route the game should connect through.</summary>
public static class RouteSelection
{
    /// <summary>
    ///     «Автоматически»: the online route with the lowest ping (the earlier one on a tie); when none is known
    ///     to be online, the first route, the main address.
    /// </summary>
    /// <param name="pingOf">The last check of a route; <c>null</c> when it was not checked.</param>
    public static ServerRouteDto? PickAuto(IReadOnlyList<ServerRouteDto> routes,
        Func<ServerRouteDto, ServerPingResult?> pingOf)
    {
        var fastest = routes
            .Select(route => (Route: route, Ping: pingOf(route)))
            .Where(probe => probe.Ping is { IsOnline: true, LatencyMs: not null })
            .OrderBy(probe => probe.Ping!.LatencyMs)
            .Select(probe => probe.Route)
            .FirstOrDefault();

        return fastest ?? routes.FirstOrDefault();
    }

    /// <summary>The route the player picked, if it still exists; <c>null</c> means «Автоматически».</summary>
    public static ServerRouteDto? FindSaved(IReadOnlyList<ServerRouteDto> routes, string? savedName) =>
        string.IsNullOrEmpty(savedName)
            ? null
            : routes.FirstOrDefault(route => route.Name == savedName);

    public static ServerRouteDto? Resolve(IReadOnlyList<ServerRouteDto> routes, string? savedName,
        Func<ServerRouteDto, ServerPingResult?> pingOf) =>
        FindSaved(routes, savedName) ?? PickAuto(routes, pingOf);
}
