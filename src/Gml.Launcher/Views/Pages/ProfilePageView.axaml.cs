using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using Gml.Launcher.ViewModels.Pages;
using ReactiveUI;
using Sentry;

namespace Gml.Launcher.Views.Pages;

public partial class ProfilePageView : ReactiveUserControl<ProfilePageViewModel>
{
    private static readonly FilePickerFileType PngFileType = new("PNG")
    {
        Patterns = new List<string> { "*.png" }
    };

    public ProfilePageView()
    {
        this.WhenActivated(disposables => { });
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnChangeSkinClick(object? sender, RoutedEventArgs e)
        => await UploadTextureAsync(isSkin: true);

    private async void OnChangeCloakClick(object? sender, RoutedEventArgs e)
        => await UploadTextureAsync(isSkin: false);

    private async Task UploadTextureAsync(bool isSkin)
    {
        if (ViewModel is null || this.GetVisualRoot() is not MainWindow mainWindow) return;

        try
        {
            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = isSkin ? "Select a skin" : "Select a cloak",
                FileTypeFilter = new[] { PngFileType }
            });

            if (files.Count != 1) return;

            await using var stream = await files[0].OpenReadAsync();

            if (isSkin)
                await ViewModel.UploadSkinAsync(stream, files[0].Name);
            else
                await ViewModel.UploadCloakAsync(stream, files[0].Name);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
