using System.Globalization;
using Gml.Client.Interfaces;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Routes;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Base;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.ViewModels.Components;

public enum RouteStatus
{
    /// <summary>Not checked yet (grey dot).</summary>
    Unknown,
    Online,
    Offline
}

/// <summary>One line of the route dropdown: «Автоматически» or a route, with its last check.</summary>
public sealed class RouteItemViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public RouteItemViewModel(ServerRouteDto route, ILocalizationService localization)
        : this(route, route.Name, localization)
    {
    }

    private RouteItemViewModel(ServerRouteDto? route, string name, ILocalizationService localization)
    {
        _localization = localization;
        Route = route;
        Name = name;
    }

    public static RouteItemViewModel Auto(ILocalizationService localization) =>
        new(null, localization.GetString(SystemConstants.ServerRouteAuto), localization);

    /// <summary><c>null</c> for «Автоматически».</summary>
    public ServerRouteDto? Route { get; }

    public string Name { get; }
    public bool IsAuto => Route is null;

    /// <summary>For «Автоматически»: the route it would use now.</summary>
    [Reactive] public string? Detail { get; private set; }

    [Reactive] public RouteStatus Status { get; private set; } = RouteStatus.Unknown;
    [Reactive] public bool IsOnline { get; private set; }
    [Reactive] public bool IsOffline { get; private set; }

    /// <summary>«54 мс» when online, empty otherwise.</summary>
    [Reactive] public string PingText { get; private set; } = string.Empty;

    /// <param name="result">The check; <c>null</c> when not checked.</param>
    /// <param name="detail">See <see cref="Detail" />.</param>
    public void Show(ServerPingResult? result, string? detail = null)
    {
        Status = result is null ? RouteStatus.Unknown : result.IsOnline ? RouteStatus.Online : RouteStatus.Offline;
        IsOnline = Status == RouteStatus.Online;
        IsOffline = Status == RouteStatus.Offline;
        PingText = result is { IsOnline: true, LatencyMs: { } latency }
            ? string.Format(CultureInfo.CurrentCulture, _localization.GetString(SystemConstants.ServerRoutePing),
                latency)
            : string.Empty;
        Detail = detail;
    }
}
