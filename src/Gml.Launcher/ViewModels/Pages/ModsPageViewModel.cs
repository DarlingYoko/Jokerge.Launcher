using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using Gml.Client;
using Gml.Client.Interfaces;
using Gml.Dto.Profile;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Exceptions;
using Gml.Launcher.Core.Mods;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Base;
using Gml.Launcher.ViewModels.Components;
using GmlCore.Interfaces.User;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.Pages;

public class ModsPageViewModel : PageViewModelBase
{
    private const double TreeIndent = 16;

    private readonly ProfileReadDto _profile;
    private readonly ILauncherUser _user;
    private readonly IGmlClientManager _gmlManager;
    private readonly ISystemService _systemService;
    private readonly OptionalModFiles _files;
    private readonly AutomaticModsStore _automaticStore;
    private readonly SemaphoreSlim _toggleLock = new(1, 1);
    private readonly TaskCompletionSource<ProfileReadInfoDto?> _profileInfo = new();
    private OptionalModGraph _graph = OptionalModGraph.Empty;
    private IReadOnlyCollection<string> _automatic = [];

    /// <summary>Every optional mod of the profile, including those shown only inside another mod's card.</summary>
    private IReadOnlyList<OptionalModItemViewModel> _allMods = [];

    /// <summary>The cards: mods no other mod needs, each with its dependencies inside.</summary>
    [Reactive] public ObservableCollection<OptionalModItemViewModel> ProfileOptionalMods { get; set; } = [];
    [Reactive] public bool ModsListIsEmpty { get; set; }
    [Reactive] public ICommand ChangeOptionalModState { get; set; }

    public string DependenciesTitle => LocalizationService.GetString(SystemConstants.ModDependenciesTitle);

    public ModsPageViewModel(IScreen screen,
        ProfileReadDto profile,
        ILauncherUser user,
        IGmlClientManager gmlManager,
        ISystemService systemService) : base(screen)
    {
        _profile = profile;
        _files = new OptionalModFiles(OptionalModFiles.DirectoryFor(gmlManager.InstallationDirectory, profile.Name));
        _automaticStore = new AutomaticModsStore(Locator.Current.GetService<IStorageService>()
                                                 ?? throw new ServiceNotFoundException(typeof(IStorageService)));
        _user = user;
        _gmlManager = gmlManager;
        _systemService = systemService;

        ChangeOptionalModState = ReactiveCommand.CreateFromTask<OptionalModItemViewModel>(ChangeModState);

        RxApp.MainThreadScheduler.Schedule(LoadData);
    }

    // The checkbox has already flipped IsSelected; this makes the files (and the dependencies) follow it.
    private async Task ChangeModState(OptionalModItemViewModel mod)
    {
        await _toggleLock.WaitAsync();
        try
        {
            var enabled = _files.EnabledKeys();
            var change = mod.IsSelected
                ? OptionalModToggle.Enable(_graph, enabled, _automatic, mod.Key)
                : OptionalModToggle.Disable(_graph, enabled, _automatic, mod.Key);

            if (change is not null)
                await ApplyAsync(change);
        }
        catch (Exception exception)
        {
            ShowError(SystemConstants.Error, exception.Message);
            SentrySdk.CaptureException(exception);
        }
        finally
        {
            Refresh();
            _toggleLock.Release();
        }
    }

    /// <summary>Downloads what has to be turned on but is missing, then renames; nothing changes if a download fails.</summary>
    private async Task ApplyAsync(OptionalModChange change)
    {
        var missing = change.ToEnable.Where(key => !_files.IsPresent(key)).ToArray();
        if (missing.Length > 0)
        {
            var profileInfo = await _profileInfo.Task;
            var toDownload = profileInfo?.Files.Where(file => missing.Contains(file.Name)).ToArray() ?? [];
            if (toDownload.Length > 0)
                await _gmlManager.DownloadFiles(toDownload);

            var stillMissing = missing.Where(key => !_files.IsPresent(key)).ToArray();
            if (stillMissing.Length > 0)
            {
                ShowError(SystemConstants.Error, string.Format(
                    LocalizationService.GetString(SystemConstants.ModDependenciesDownloadFailed),
                    string.Join(", ", stillMissing.Select(TitleOf))));
                return;
            }
        }

        _files.Apply(change);
        _automatic = change.Automatic;
        await _automaticStore.SetAsync(_profile.Name, _automatic);
    }

    /// <summary>Shows the files' state: what is on, and what is locked on by another mod.</summary>
    private void Refresh()
    {
        var enabled = _files.EnabledKeys();
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var mod in _allMods)
            {
                var neededBy = _graph.NeededBy(mod.Key, enabled);
                mod.IsSelected = enabled.Contains(mod.Key);
                mod.IsLocked = mod.IsSelected && neededBy.Count > 0;
                mod.CanToggle = !mod.IsLocked;
                mod.HasNeededBy = neededBy.Count > 0;
                mod.NeededByText = neededBy.Count > 0
                    ? string.Format(LocalizationService.GetString(SystemConstants.ModNeededBy),
                        string.Join(", ", neededBy.Select(TitleOf)))
                    : string.Empty;
            }
        });
    }

    private string TitleOf(string key) =>
        _allMods.FirstOrDefault(mod => mod.Key == key)?.Title ?? key;

    private void LoadData()
    {
        _ = ExecuteFromNewThread(async () =>
        {
            try
            {
                var details = (await _gmlManager.GetOptionalModsInfo(_user.AccessToken)).Data ?? [];
                var optionalMods = (await _gmlManager.GetOptionalMods(_profile.Name, _user.AccessToken)).Data ?? [];
                _automatic = await _automaticStore.GetAsync(_profile.Name);

                var detailsByKey = details.Where(d => !string.IsNullOrEmpty(d.Key))
                    .GroupBy(d => d.Key).ToDictionary(g => g.Key, g => g.First());
                var keys = optionalMods.Select(mod => $"{mod.Name}.jar").Distinct().ToArray();
                _graph = OptionalModGraphs.FromDetails(details, keys);

                string Title(string key) =>
                    detailsByKey.TryGetValue(key, out var info) && !string.IsNullOrWhiteSpace(info.Title)
                        ? info.Title
                        : key[..^".jar".Length];

                string Description(string key) =>
                    detailsByKey.TryGetValue(key, out var info) && !string.IsNullOrWhiteSpace(info.Description)
                        ? info.Description
                        : "Не указано";

                var items = keys.ToDictionary(key => key,
                    key => new OptionalModItemViewModel(key, Title(key), Description(key)));
                foreach (var item in items.Values)
                {
                    item.Dependencies = _graph.Tree(item.Key)
                        .Select(node => new DependencyRow(items[node.Key],
                            new Thickness(node.Depth * TreeIndent, 0, 0, 0)))
                        .ToArray();
                }

                _allMods = items.Values.ToArray();
                var cards = _graph.TopLevel(keys).Select(key => items[key]).ToArray();

                Dispatcher.UIThread.Invoke(() =>
                {
                    ProfileOptionalMods = new ObservableCollection<OptionalModItemViewModel>(cards);
                    ModsListIsEmpty = cards.Length == 0;
                });
                Refresh();

                await LoadProfileInfo();
                await RepairAsync();
            }
            catch (Exception exception)
            {
                _profileInfo.TrySetResult(null);
                SentrySdk.CaptureException(exception);
            }
        });
    }

    private async Task LoadProfileInfo()
    {
        try
        {
            var profileInfo = await _gmlManager.GetProfileInfo(new ProfileCreateInfoDto
            {
                ProfileName = _profile.Name,
                RamSize = 0,
                OsType = ((int)_systemService.GetOsType()).ToString(),
                OsArchitecture = _systemService.GetOsArchitecture(),
                UserAccessToken = _user.AccessToken,
                UserName = _user.Name,
                UserUuid = _user.Uuid
            });
            _profileInfo.TrySetResult(profileInfo?.Data);
        }
        catch (Exception exception)
        {
            // Without the file list a missing mod cannot be downloaded; turning on present ones still works.
            _profileInfo.TrySetResult(null);
            SentrySdk.CaptureException(exception);
        }
    }

    // An enabled mod may have got a new dependency since the player last turned it on.
    private async Task RepairAsync()
    {
        await _toggleLock.WaitAsync();
        try
        {
            var change = OptionalModToggle.Repair(_graph, _files.EnabledKeys(), _automatic);
            if (!change.IsEmpty)
                await ApplyAsync(change);
        }
        finally
        {
            Refresh();
            _toggleLock.Release();
        }
    }
}
