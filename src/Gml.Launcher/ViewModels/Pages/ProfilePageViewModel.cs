using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client;
using Gml.Client.Interfaces;
using Gml.Client.Models;
using Gml.Launcher.Assets;
using Gml.Launcher.Core;
using Gml.Launcher.Core.Exceptions;
using Gml.Launcher.Core.Services;
using Gml.Launcher.ViewModels.Base;
using GmlCore.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sentry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Splat;

namespace Gml.Launcher.ViewModels.Pages;

public class ProfilePageViewModel : PageViewModelBase
{
    private readonly IGmlClientManager _manager;
    private readonly ISkinUploadService _skinUploadService;
    [Reactive] public string TextureUrl { get; set; }
    [Reactive] public ILauncherUser LauncherUser { get; set; }
    [Reactive] public bool IsUploading { get; set; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    internal ProfilePageViewModel(
        IScreen screen,
        ILauncherUser launcherUser,
        IGmlClientManager manager,
        ILocalizationService? localizationService = null,
        ISkinUploadService? skinUploadService = null) : base(screen,
        localizationService)
    {
        LauncherUser = launcherUser ?? throw new ArgumentNullException(nameof(launcherUser));
        _manager = manager;
        _skinUploadService = skinUploadService
                              ?? Locator.Current.GetService<ISkinUploadService>()
                              ?? throw new ServiceNotFoundException(typeof(ISkinUploadService));

        ResetCommand = ReactiveCommand.CreateFromTask(ResetTextureAsync);

        RxApp.TaskpoolScheduler.Schedule(LoadData);
    }

    public new string Title => LocalizationService.GetString(SystemConstants.MainPageTitle);

    private async void LoadData()
    {
        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss:fff}] Loading texture data...]");
            var userTextureInfo = await _manager.GetTexturesByName(LauncherUser.Name);

            if (userTextureInfo is null)
                return;

            var skinUrl = userTextureInfo.FullSkinUrl;

            TextureUrl = string.IsNullOrEmpty(skinUrl)
                ? string.Empty
                : $"{skinUrl}{(skinUrl.Contains('?') ? '&' : '?')}_={DateTime.UtcNow.Ticks}";

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss:fff}] Textures updated: {TextureUrl}");
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    public Task UploadSkinAsync(Stream fileStream, string fileName)
        => UploadTextureAsync(fileStream, fileName, true);

    public Task UploadCloakAsync(Stream fileStream, string fileName)
        => UploadTextureAsync(fileStream, fileName, false);

    private async Task UploadTextureAsync(Stream fileStream, string fileName, bool isSkin)
    {
        if (IsUploading) return;

        IsUploading = true;

        try
        {
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            if (!IsValidTexture(memoryStream, isSkin))
            {
                ShowError(SystemConstants.Error,
                    LocalizationService.GetString(isSkin
                        ? SystemConstants.InvalidSkinFormat
                        : SystemConstants.InvalidCloakFormat));
                return;
            }

            memoryStream.Position = 0;

            var (isSuccess, error) = isSkin
                ? await _skinUploadService.UploadSkinAsync(LauncherUser.Name, LauncherUser.AccessToken, memoryStream,
                    fileName, CancellationToken.None)
                : await _skinUploadService.UploadCloakAsync(LauncherUser.Name, LauncherUser.AccessToken,
                    memoryStream, fileName, CancellationToken.None);

            if (isSuccess)
            {
                LoadData();
                ShowSuccess(SystemConstants.Success,
                    LocalizationService.GetString(isSkin ? SystemConstants.SkinUploaded : SystemConstants.CloakUploaded));
            }
            else
            {
                ShowError(SystemConstants.Error, error ?? LocalizationService.GetString(SystemConstants.Error));
            }
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            ShowError(SystemConstants.Error, LocalizationService.GetString(SystemConstants.Error));
        }
        finally
        {
            IsUploading = false;
        }
    }

    private async Task ResetTextureAsync()
    {
        if (IsUploading) return;

        IsUploading = true;

        try
        {
            var skinTask = _skinUploadService.ResetSkinAsync(LauncherUser.Name, LauncherUser.AccessToken,
                CancellationToken.None);
            var cloakTask = _skinUploadService.ResetCloakAsync(LauncherUser.Name, LauncherUser.AccessToken,
                CancellationToken.None);

            await Task.WhenAll(skinTask, cloakTask);

            var (skinSuccess, skinError) = skinTask.Result;
            var (cloakSuccess, cloakError) = cloakTask.Result;

            if (skinSuccess && cloakSuccess)
            {
                LoadData();
                ShowSuccess(SystemConstants.Success, LocalizationService.GetString(SystemConstants.TextureReset));
            }
            else
            {
                var error = skinError ?? cloakError ?? LocalizationService.GetString(SystemConstants.Error);
                ShowError(SystemConstants.Error, error);
            }
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            ShowError(SystemConstants.Error, LocalizationService.GetString(SystemConstants.Error));
        }
        finally
        {
            IsUploading = false;
        }
    }

    private static bool IsValidTexture(Stream stream, bool isSkin)
    {
        try
        {
            var info = Image.Identify(stream);

            if (info is null || info.Metadata.DecodedImageFormat is not PngFormat)
                return false;

            return isSkin
                ? info.Width == 64 && (info.Height == 64 || info.Height == 32)
                : info.Width == 64 && info.Height == 32;
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            return false;
        }
    }
}
