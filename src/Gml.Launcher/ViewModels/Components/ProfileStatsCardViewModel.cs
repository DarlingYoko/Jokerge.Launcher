using System;
using System.Collections.Generic;
using System.Globalization;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Core.Stats;
using Gml.Launcher.ViewModels.Base;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.ViewModels.Components;

/// <summary>
///     One profile on the profile page: the player's progress from that server's stats site and how long the
///     profile ran on this computer.
/// </summary>
public class ProfileStatsCardViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public ProfileStatsCardViewModel(string name, string displayName, string? iconBase64, string? gameVersion,
        ILocalizationService localization)
    {
        _localization = localization;
        Name = name;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        IconBase64 = string.IsNullOrWhiteSpace(iconBase64) ? null : iconBase64;
        GameVersion = gameVersion ?? string.Empty;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string? IconBase64 { get; }
    public bool HasIcon => IconBase64 is not null;
    public string GameVersion { get; }

    [Reactive] public bool IsLoading { get; private set; } = true;
    [Reactive] public IReadOnlyList<StatTile> Tiles { get; private set; } = [];

    /// <returns>Whether there is anything to show; a profile without stats is left out of the page.</returns>
    /// <param name="clientTime">How long the profile's client ran on this computer; <c>null</c> when unknown.</param>
    public bool Apply(ServerPlayerStats? stats, TimeSpan? clientTime)
    {
        var culture = Assets.Resources.Resources.Culture ?? CultureInfo.CurrentUICulture;
        Tiles = stats is null
            ? []
            : ServerStatsTiles.Build(stats, clientTime, _localization.GetString, culture, TimeZoneInfo.Local,
                DateTimeOffset.Now);
        IsLoading = false;
        return stats is not null;
    }
}
