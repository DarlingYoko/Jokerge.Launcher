using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Routes;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Base;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;

namespace Gml.Launcher.ViewModels.Components;

/// <summary>
///     The route picker on the home page: «Автоматически» plus each entry point to the profile's server, with
///     status and ping measured from this computer. Hidden when the profile has fewer than two routes.
/// </summary>
public sealed class ServerRoutesViewModel : ViewModelBase
{
    private readonly IServerRoutesSource _source;
    private readonly IMinecraftStatusPinger _pinger;
    private readonly RouteChoiceStore _choices;
    private readonly ILocalizationService _localization;

    // Bumped on every load, so answers for a profile the player already left are dropped.
    private int _generation;
    private string? _profileName;
    private IReadOnlyList<ServerRouteDto> _routes = [];
    private bool _isRestoring;
    private RouteItemViewModel? _selectedItem;

    public ServerRoutesViewModel(IServerRoutesSource source, IMinecraftStatusPinger pinger, RouteChoiceStore choices,
        ILocalizationService localization)
    {
        _source = source;
        _pinger = pinger;
        _choices = choices;
        _localization = localization;

        Title = localization.GetString(SystemConstants.ServerRoutesTitle);
        CheckText = localization.GetString(SystemConstants.ServerRoutesCheck);

        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync,
            this.WhenAnyValue(vm => vm.IsChecking, vm => vm.IsVisible, (checking, visible) => !checking && visible));
    }

    public string Title { get; }

    [Reactive] public IReadOnlyList<RouteItemViewModel> Items { get; private set; } = [];
    [Reactive] public bool IsVisible { get; private set; }
    [Reactive] public bool IsChecking { get; private set; }
    [Reactive] public string CheckText { get; private set; }

    public ReactiveCommand<Unit, Unit> CheckCommand { get; }

    /// <summary>Picking an item saves it for the current profile; «Автоматически» is saved as no choice.</summary>
    public RouteItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            if (!_isRestoring && value is not null && _profileName is { } profileName)
                _ = SaveChoiceAsync(profileName, value.IsAuto ? null : value.Name);
        }
    }

    /// <summary>Shows the routes of <paramref name="profileName" /> and checks them once.</summary>
    public async Task LoadAsync(string? profileName)
    {
        var generation = ++_generation;
        _profileName = profileName;
        _routes = [];
        SetChecking(false);
        Replace([], null);
        IsVisible = false;

        if (profileName is null)
            return;

        var routes = await _source.GetRoutesAsync(profileName);
        if (generation != _generation || routes is not { Count: > 1 })
            return;

        var saved = await _choices.GetAsync(profileName);
        if (generation != _generation)
            return;

        _routes = routes;
        var items = routes.Select(route => new RouteItemViewModel(route, _localization)).ToList();
        items.Insert(0, RouteItemViewModel.Auto(_localization));
        var savedRoute = RouteSelection.FindSaved(routes, saved);
        Replace(items, items.FirstOrDefault(item => item.Route is not null && item.Route == savedRoute) ?? items[0]);
        IsVisible = true;

        await PingAsync(generation);
    }

    /// <summary>Pings every route at once and updates the items.</summary>
    public Task CheckAsync() => PingAsync(_generation);

    private async Task PingAsync(int generation)
    {
        var routes = _routes;
        var items = Items;
        if (routes.Count == 0)
            return;

        SetChecking(true);
        try
        {
            var results = await _pinger.PingAllAsync(routes);
            if (generation != _generation)
                return;

            for (var index = 0; index < routes.Count; index++)
                items[index + 1].Show(results[index]);

            var resultByRoute = routes.Zip(results).ToDictionary(pair => pair.First, pair => pair.Second);
            var pick = RouteSelection.PickAuto(routes, resultByRoute.GetValueOrDefault)!;
            items[0].Show(resultByRoute[pick], pick.Name);
        }
        finally
        {
            if (generation == _generation)
                SetChecking(false);
        }
    }

    private void Replace(IReadOnlyList<RouteItemViewModel> items, RouteItemViewModel? selected)
    {
        // Swapping the list makes the dropdown clear its selection; that is not the player's choice.
        _isRestoring = true;
        try
        {
            Items = items;
            SelectedItem = selected;
        }
        finally
        {
            _isRestoring = false;
        }
    }

    private void SetChecking(bool isChecking)
    {
        IsChecking = isChecking;
        CheckText = _localization.GetString(isChecking
            ? SystemConstants.ServerRoutesChecking
            : SystemConstants.ServerRoutesCheck);
    }

    private async Task SaveChoiceAsync(string profileName, string? routeName)
    {
        try
        {
            await _choices.SetAsync(profileName, routeName);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
