using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Gml.Client;
using Gml.Client.Interfaces;
using Gml.Launcher.Assets.Resources;
using Gml.Launcher.Core.Diagnostics;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using Gml.Launcher.ViewModels.Base;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using Splat;

namespace Gml.Launcher.ViewModels.Pages;

public class SettingsPageViewModel : PageViewModelBase
{
    private const int HighRamThreshold = 16384;
    private readonly IGmlClientManager _gmlManager;
    private readonly ISettingsService _settingsService;
    private readonly ILauncherUser? _user;
    private readonly IDiagnosticsService? _diagnosticsService;
    private readonly ISystemService? _systemService;
    private readonly int? _profileRecommendedRamMb;

    private double _ramValue;
    private int _windowHeight;
    private int _windowWidth;

    public SettingsPageViewModel(
        IScreen screen,
        ILocalizationService? localizationService,
        ISettingsService settingsService,
        IGmlClientManager gmlManager,
        ILauncherUser? user = null,
        IDiagnosticsService? diagnosticsService = null,
        int? profileRecommendedRamMb = null,
        ISystemService? systemService = null) : base(screen, localizationService)
    {
        MainViewModel = (MainWindowViewModel)screen;
        _settingsService = settingsService;
        _gmlManager = gmlManager;
        _user = user;
        _diagnosticsService = diagnosticsService ?? Locator.Current.GetService<IDiagnosticsService>();
        _systemService = systemService ?? Locator.Current.GetService<ISystemService>();
        _profileRecommendedRamMb = profileRecommendedRamMb;

        this.WhenAnyValue(x => x.DynamicRamValue)
            .Where(isDynamic => isDynamic)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(_ => DescribeAutoRam())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(preview => AutoRamPreview = preview);

        RunDiagnosticsCommand = ReactiveCommand.CreateFromTask(RunDiagnostics,
            this.WhenAnyValue(x => x.IsDiagnosticsRunning, running => !running && CanRunDiagnostics));
        CloseDiagnosticsCommand = ReactiveCommand.Create(() => { IsDiagnosticsOpen = false; });

        this.WhenAnyValue(
                x => x.RamValue,
                x => x.WindowWidth,
                x => x.WindowHeight,
                x => x.FullScreen,
                x => x.DynamicRamValue,
                x => x.SelectedLanguage
            )
            .Where(ValidateParams)
            .Throttle(TimeSpan.FromMilliseconds(400), RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(SaveSettings);

        this.WhenAnyValue(x => x.SelectedLanguage)
            .Skip(2)
            .Subscribe(ChangeLanguage);

        this.WhenAnyValue(x => x.Settings)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(UpdateSettings);

        RxApp.TaskpoolScheduler.Schedule(LoadSettings);
    }

    private bool ValidateParams(
        (
            double ramValue,
            string width,
            string height,
            bool isFullScreen,
            bool isDynamicRam,
            Language? selectedLanguage)
        update)
    {
        if (update.ramValue <= 0)
        {
            return false;
        }

        if (!int.TryParse(update.width, out var width) || width <= 0)
        {
            return false;
        }

        if (!int.TryParse(update.height, out var height) || height <= 0)
        {
            return false;
        }

        return true;
    }

    [Reactive] public bool DynamicRamValue { get; set; }
    [Reactive] public bool FullScreen { get; set; }
    [Reactive] public ulong MinRamValue { get; set; }
    [Reactive] public ulong MaxRamValue { get; set; }
    [Reactive] public ulong RamTickValue { get; set; }
    [Reactive] public Language? SelectedLanguage { get; set; }
    [Reactive] public string? InstallationFolder { get; set; }
    [Reactive] public string? RamValueView { get; set; }
    [Reactive] private SettingsInfo? Settings { get; set; }

    // «Диагностика»: one click collects the report, sends it to the server and shows it for copying.
    /// <summary>With «автоматически» on: how much the game would get on this PC right now.</summary>
    [Reactive] public string? AutoRamPreview { get; private set; }

    [Reactive] public bool IsDiagnosticsOpen { get; set; }
    [Reactive] public bool IsDiagnosticsRunning { get; private set; }
    [Reactive] public string? DiagnosticsReportText { get; private set; }
    [Reactive] public string? DiagnosticsStatus { get; private set; }
    public bool CanRunDiagnostics => _user is not null && _diagnosticsService is not null;
    public ICommand RunDiagnosticsCommand { get; }
    public ICommand CloseDiagnosticsCommand { get; }

    public double RamValue
    {
        get => _ramValue;
        set
        {
            if (!(Math.Abs(value - _ramValue) > 0.0)) return;

            _ramValue = Round(value, 8);
            RamValueView = Convert.ToInt32(_ramValue).ToString(CultureInfo.InvariantCulture);
            this.RaisePropertyChanged();
            return;

            double Round(double value, double step)
            {
                var offset = value % step;
                return offset >= step / 2.0
                    ? value + (step - offset)
                    : value - offset;
            }
        }
    }

    public string WindowWidth
    {
        get => _windowWidth.ToString();
        set
        {
            var isNumeric = int.TryParse(string.Concat(value.Where(char.IsDigit)), out var numericValue);
            if (isNumeric)
                this.RaiseAndSetIfChanged(ref _windowWidth, numericValue);
            else
                this.RaiseAndSetIfChanged(ref _windowWidth, 600);
        }
    }

    public string WindowHeight
    {
        get => _windowHeight.ToString();
        set
        {
            var isNumeric = int.TryParse(string.Concat(value.Where(char.IsDigit)), out var numericValue);
            if (isNumeric)
                this.RaiseAndSetIfChanged(ref _windowHeight, numericValue);
            else
                this.RaiseAndSetIfChanged(ref _windowWidth, 900);
        }
    }

    [Reactive] public ObservableCollection<Language> AvailableLanguages { get; set; } = [];
    public MainWindowViewModel MainViewModel { get; }

    public string LauncherVersion { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0));

    private async Task RunDiagnostics()
    {
        if (_user is null || _diagnosticsService is null) return;

        IsDiagnosticsOpen = true;
        IsDiagnosticsRunning = true;
        DiagnosticsReportText = null;
        DiagnosticsStatus = LocalizationService.GetString(DiagnosticsResourceKeys.DiagnosticsCollecting);

        try
        {
            var user = _user;
            var report = await Task.Run(() => _diagnosticsService.CollectAsync(user));
            DiagnosticsReportText = report.Text;

            var result = await _diagnosticsService.SendAsync(user, report);
            DiagnosticsStatus = result.Success
                ? string.Format(LocalizationService.GetString(DiagnosticsResourceKeys.DiagnosticsSent),
                    result.Id?.ToString("N")[..8] ?? "—")
                : string.Format(LocalizationService.GetString(DiagnosticsResourceKeys.DiagnosticsSendFailed),
                    result.Error);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            DiagnosticsStatus = string.Format(
                LocalizationService.GetString(DiagnosticsResourceKeys.DiagnosticsSendFailed), exception.Message);
        }
        finally
        {
            IsDiagnosticsRunning = false;
        }
    }

    private string? DescribeAutoRam()
    {
        if (_systemService is null) return null;

        try
        {
            var freeRam = PreLaunchMeasurements.FreeRamMb(_systemService);
            var plan = RamSelection.Plan(true, 0, _profileRecommendedRamMb, (long)_systemService.GetMaxRam(),
                freeRam);

            return string.Format(LocalizationService.GetString(DiagnosticsResourceKeys.RamAutoPreview),
                plan.AllocatedMb, plan.RecommendedMb, freeRam?.ToString() ?? "?");
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            return null;
        }
    }

    internal void NotifyDiagnosticsCopied()
    {
        ShowSuccess(DiagnosticsResourceKeys.Diagnostics,
            LocalizationService.GetString(DiagnosticsResourceKeys.DiagnosticsCopied));
    }

    private void ChangeLanguage(Language? language)
    {
        if (language == null) return;

        Resources.Culture = language.Culture;

        GoBackCommand.Execute(Unit.Default);
    }

    internal void ChangeFolder()
    {
        if (InstallationFolder != null)
            _gmlManager.ChangeInstallationFolder(InstallationFolder);

        _settingsService.UpdateInstallationDirectory(InstallationFolder);
    }

    private void UpdateSettings(SettingsInfo settings)
    {
        WindowWidth = settings.GameWidth.ToString();
        WindowHeight = settings.GameHeight.ToString();
        RamValue = settings.RamValue;
        DynamicRamValue = settings.IsDynamicRam;
        FullScreen = settings.FullScreen;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(c => c.Culture.Name == settings.LanguageCode);
        InstallationFolder = _gmlManager.InstallationDirectory;

        MinRamValue = (ulong)(MaxRamValue > HighRamThreshold ? 1024 : 512);
        RamTickValue = (ulong)(MaxRamValue > HighRamThreshold ? 1024 : 512);
    }

    private async void LoadSettings()
    {
        try
        {
            AvailableLanguages = new ObservableCollection<Language>(_settingsService.GetAvailableLanguages());
            MaxRamValue = _settingsService.GetMaxRam();

            Settings = await _settingsService.GetSettings();
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    private async void SaveSettings(
        (double ramValue, string width, string height, bool isFullScreen, bool isDynamicRam, Language? selectedLanguage)
            update)
    {
        try
        {
            var newSettings = new SettingsInfo(
                int.Parse(update.width),
                int.Parse(update.height),
                update.isFullScreen,
                update.isDynamicRam,
                update.ramValue, update.selectedLanguage?.Culture.Name);

            await _settingsService.UpdateSettingsAsync(newSettings);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
