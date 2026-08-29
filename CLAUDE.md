# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

**Jokerge Launcher** is a cross-platform Minecraft launcher built on the GamerVII/Gml stack. It is a
customized fork of the upstream `Gml.Launcher` (GamerVII Minecraft Launcher): branding, host, and
folder name have been rebranded to "Jokerge", but the architecture is unchanged from upstream.

Tech stack: **.NET 8.0**, **Avalonia 11.3** UI (Fluent theme), **ReactiveUI** (MVVM), **Splat** for
service location. Targets Windows, Linux, and macOS (x64/arm64).

## Submodules — clone recursively

Two of the four `src/` projects are git submodules and the build will fail without them:

- `src/Gml.Client` — the Gml backend client library (talks to the GML web API; provides `GmlClientManager`, `GameLoader`).
- `src/GamerVII.Notification.Avalonia` — the toast/notification UI library.

After a fresh clone or if these directories are empty:

```bash
git submodule update --init --recursive
```

Or run `load-repositories.bat` / `load-repositories.sh`. `src/L1.Avalonia.Gif` (GIF rendering
control) is a vendored in-tree project, not a submodule.

## Build / Run / Publish

```bash
dotnet restore
dotnet build
dotnet run --project src/Gml.Launcher/Gml.Launcher.csproj
```

Solution file: `Gml.Launcher.sln`. There is no test project in this repository.

Release publishing (mirrors CI in `.github/workflows/build-windows.yml`, single-file per RID):

```bash
dotnet publish src/Gml.Launcher/Gml.Launcher.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true -o ./publish/win-x64
```

CI (`.github/workflows/ci.yml`) fans out to per-platform reusable workflows and publishes a combined
`Launcher-All` artifact. `-skip-update` is a supported runtime arg that skips the self-update check
(always skipped in Debug builds).

## Configuration tokens (important)

`src/Gml.Launcher/Assets/Resources/ResourceKeysDictionary.cs` holds the launcher's identity:
`Host`, `SecondaryHost`, and `FolderName` (the installation subfolder). The committed
`ResourceKeysDictionary.Template.cs` (excluded from compile via `Compile Remove` in the csproj) is the
template with `{{HOST}}`, `{{HOST_SECONDARY}}`, `{{FOLDER_NAME}}` placeholders that the GML backend
substitutes when it packages a launcher. When editing host/folder config, change
`ResourceKeysDictionary.cs`; keep the `.Template.cs` placeholders intact.

## Architecture

### Startup flow
`Program.cs` → `BuildAvaloniaApp()` → `ServiceLocator.RegisterServices()` (registers all services into
the Splat `Locator`) → `App.OnFrameworkInitializationCompleted()` shows `SplashScreen`, runs
`SplashScreenViewModel.InitializeAsync()` (API status, auth/session check, update check), then swaps
the main window in via `splashScreen.GetMainWindow()`.

### Dependency injection — Splat service locator
Services are registered as constants in `Core/Extensions/ServiceLocator.cs` and resolved through
`Locator.Current.GetService<T>()`. ViewModels take services as **optional constructor params that fall
back to the locator** (see the `?? Locator.Current.GetService<T>() ?? throw new ServiceNotFoundException(...)`
pattern) — this keeps them unit-test-friendly while working via the locator at runtime. There is no
constructor-injection container.

### MVVM + navigation (ReactiveUI)
- `MainWindowViewModel` implements `IScreen` and owns a `RoutingState Router`. Pages are
  `IRoutableViewModel` deriving from `PageViewModelBase`; navigate with `Router.Navigate.Execute(new SomePageViewModel(this, ...))`.
- `Core/Helpers/AppViewLocator.cs` maps each page ViewModel to its View (`switch` expression) — **add
  new pages here** or navigation throws `ArgumentOutOfRangeException`.
- Views are `.axaml` + `.axaml.cs` code-behind under `Views/Pages`, `Views/Components`,
  `Views/SplashScreen`. Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`).
- `ReactiveUI.Fody` is used — properties marked `[Reactive]` auto-generate change notifications.
- `PageViewModelBase` provides shared helpers: `ShowError` / `ShowSuccess` (toasts via the
  `NotificationMessageManager`), `ExecuteFromNewThread`, and `OpenLinkCommand`.

### Localization
Two constant classes hold **resource keys** (not display strings): `Assets/ResourceKeysDictionary.cs`
and `Core/SystemConstants.cs`. The strings live in `.resx` files (`Assets/Resources/Resources.resx`,
`.en.resx`, `.ru.resx`; default culture `ru-RU`). Resolve at runtime via
`ILocalizationService.GetString(key)` (implemented by `ResourceLocalizationService`). Chosen language
is persisted in settings and applied at startup in `ServiceLocator.CheckAndChangeLanguage`.

### Persistence & services (`Core/Services`)
Interface + implementation pairs (wired as `DependentUpon` in the csproj):
- `IStorageService` / `LocalStorageService` — key/value store backed by **sqlite-net-pcl** (`identifier.sqlite`); keys in `StorageConstants.cs`.
- `ISettingsService` / `SettingsService`, `ISystemService` / `SystemService` (OS/hardware detection, folders).
- `IBackendChecker` / `BackendChecker` — is the GML backend reachable / offline mode.
- `IVpnChecker` / `VpnChecker` — warns when a VPN tunnel is detected.
- `LogHandler`, `SkinService`. Errors are reported to **Sentry** (`SentrySdk`) throughout.

### Gml integration
`GmlClientManager` (from the `Gml.Client` submodule) is the core bridge to the GML backend for auth,
profile download/verification, and game launch. It is constructed in `ServiceLocator.RegisterGmlManager`
with the install directory, gateway (from `CheckApiStatus(Host, SecondaryHost)`), a `GameLoader`, and
the OS type, then registered as `IGmlClientManager`.

## Conventions
- 4-space indent for C#; 2-space for `.csproj`/xml/json/yaml (`.editorconfig`). `Nullable` is enabled.
- Register every new service in `ServiceLocator`; map every new page ViewModel in `AppViewLocator`.
- Add UI text as a `.resx` entry + a key constant, referenced via `ILocalizationService`, never a hardcoded literal.
