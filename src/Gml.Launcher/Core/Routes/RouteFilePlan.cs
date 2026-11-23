using System.Collections.Generic;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Routes;

public enum RouteFilePlanKind
{
    /// <summary>The routes are unknown (offline, API down): leave whatever file is there.</summary>
    Keep,

    /// <summary>The profile has no routes: no file, so the mod does nothing.</summary>
    Remove,

    /// <summary>Write the routes and the target.</summary>
    Write
}

/// <summary>What to do with the route file before a launch.</summary>
public sealed class RouteFilePlan
{
    private RouteFilePlan(RouteFilePlanKind kind, IReadOnlyList<ServerRouteDto> routes, ServerRouteDto? target)
    {
        Kind = kind;
        Routes = routes;
        Target = target;
    }

    public static RouteFilePlan Keep { get; } = new(RouteFilePlanKind.Keep, [], null);
    public static RouteFilePlan Remove { get; } = new(RouteFilePlanKind.Remove, [], null);

    public RouteFilePlanKind Kind { get; }

    /// <summary>All routes, main address first; empty unless <see cref="RouteFilePlanKind.Write" />.</summary>
    public IReadOnlyList<ServerRouteDto> Routes { get; }

    /// <summary>The route to connect through; set only for <see cref="RouteFilePlanKind.Write" />.</summary>
    public ServerRouteDto? Target { get; }

    public static RouteFilePlan Write(IReadOnlyList<ServerRouteDto> routes, ServerRouteDto target) =>
        new(RouteFilePlanKind.Write, routes, target);
}
