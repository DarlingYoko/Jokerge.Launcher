using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Gml.Client;
using Gml.Client.Interfaces;
using Gml.Launcher.Assets;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using Sentry;
using Splat;

namespace Gml.Launcher.Core.Extensions;

public static class ServiceLocator
{
    public static AppBuilder RegisterServices(this AppBuilder builder, string[] arguments)
    {
        var systemService = new SystemService();

        var installationDirectory =
            Path.Combine(systemService.GetApplicationFolder(), ResourceKeysDictionary.FolderName);

        RegisterLocalizationService();
        RegisterSystemService(systemService);
        RegisterLogHelper(systemService);
        var manager = RegisterGmlManager(systemService, installationDirectory, arguments);
        var storageService = RegisterStorage();

        var fileSyncLogService = new FileSyncLogService(manager);
        Locator.CurrentMutable.RegisterConstant(fileSyncLogService, typeof(IFileSyncLogService));
        manager.FilesSynced.Subscribe(fileSyncLogService.LogSync);

        CheckAndChangeInstallationFolder(storageService, manager);
        CheckAndChangeLanguage(storageService, systemService);
        Locator.CurrentMutable.RegisterConstant(new VpnChecker(), typeof(IVpnChecker));
        Locator.CurrentMutable.RegisterConstant(new BackendChecker(), typeof(IBackendChecker));
        Locator.CurrentMutable.RegisterConstant(new SettingsService(
                GetRequiredService<ISystemService>(),
                GetRequiredService<IStorageService>()
            ),
            typeof(ISettingsService)
        );
        Locator.CurrentMutable.RegisterConstant(new SkinUploadService(manager), typeof(ISkinUploadService));

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            SentrySdk.CaptureException((Exception)args.ExceptionObject);
        };

        Debug.WriteLine($"[Gml][{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Configuring ended");

        return builder;
    }

    private static T GetRequiredService<T>()
    {
        return Locator.Current.GetService<T>() ?? throw new Exception();
    }

    private static void RegisterLogHelper(SystemService systemService)
    {
        Locator.CurrentMutable.RegisterConstant(new LogHandler());
    }

    private static void CheckAndChangeLanguage(LocalStorageService storageService, SystemService systemService)
    {
        var data = storageService.GetAsync<SettingsInfo>(StorageConstants.Settings).Result;

        if (data != null && !string.IsNullOrEmpty(data.LanguageCode))
            Assets.Resources.Resources.Culture = systemService
                .GetAvailableLanguages()
                .FirstOrDefault(c => c.Culture.Name == data.LanguageCode)?
                .Culture ?? new CultureInfo("ru-RU");
    }

    private static LocalStorageService RegisterStorage()
    {
        var storageService = new LocalStorageService();
        Locator.CurrentMutable.RegisterConstant(storageService, typeof(IStorageService));

        return storageService;
    }

    private static void CheckAndChangeInstallationFolder(LocalStorageService storageService, GmlClientManager manager)
    {
        var installationDirectory = storageService.GetAsync<string>(StorageConstants.InstallationDirectory).Result;

        if (!string.IsNullOrEmpty(installationDirectory)) manager.ChangeInstallationFolder(installationDirectory);
    }

    private static GmlClientManager RegisterGmlManager(SystemService systemService, string installationDirectory,
        string[] arguments)
    {
        // Deliberately skip GmlClientManager.CheckApiStatus's ping + blocking HTTP probe here: this
        // method runs synchronously inside BuildAvaloniaApp, before Avalonia's desktop lifetime (and
        // therefore any window, including the splash screen) has started — so blocking on it froze
        // the app with no window at all for as long as the backend took to respond (or timed out).
        // Backend reachability is already checked asynchronously, with the splash screen visible, by
        // SplashScreenViewModel.InitializeAsync -> IBackendChecker.UpdateBackendStatus.
        var manager = new GmlClientManager(
            installationDirectory,
            ResourceKeysDictionary.Host,
            new GameLoader(),
            ResourceKeysDictionary.FolderName,
            systemService.GetOsType());
#if DEBUG
        manager.SkipUpdate = true;
#else
        manager.SkipUpdate = arguments.Contains("-skip-update");
#endif

        Locator.CurrentMutable.RegisterConstant(manager, typeof(IGmlClientManager));

        return manager;
    }

    private static void RegisterSystemService(SystemService systemService)
    {
        Locator.CurrentMutable.RegisterConstant(systemService, typeof(ISystemService));

    }

    private static void RegisterLocalizationService()
    {
        var service = new ResourceLocalizationService();
        Locator.CurrentMutable.RegisterConstant(service, typeof(ILocalizationService));
    }
}
