using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using Gml.Client.Interfaces;
using Gml.Launcher.ViewModels.Base;
using ReactiveUI;

namespace Gml.Launcher.ViewModels.Components;

public class ListViewModel : ViewModelBase
{
    internal readonly Subject<LauncherProfileReadDto?> ProfileChanged = new();


    private ObservableCollection<LauncherProfileReadDto>? _profiles;
    private LauncherProfileReadDto? _selectedProfile;

    public ObservableCollection<LauncherProfileReadDto>? Profiles
    {
        get => _profiles;
        set
        {
            this.RaiseAndSetIfChanged(ref _profiles, value);
            this.RaisePropertyChanged(nameof(IsNotLoaded));
            this.RaisePropertyChanged(nameof(HasItems));
            this.RaisePropertyChanged(nameof(HasSelectedItem));
        }
    }

    public LauncherProfileReadDto? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfile, value);
            this.RaisePropertyChanged(nameof(HasSelectedItem));

            ProfileChanged.OnNext(value);
        }
    }

    public bool IsNotLoaded => _profiles == null;
    public bool HasItems => _profiles != null && _profiles.Any();
    public bool HasSelectedItem => _selectedProfile != null;
}
