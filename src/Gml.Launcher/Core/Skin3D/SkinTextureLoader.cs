using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Gml.Client;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Gml.Launcher.Core.Skin3D;

/// <summary>A downloaded skin or cape, ready to draw.</summary>
/// <param name="HasPixels">False for the fully transparent cape the API returns to players without one.</param>
public sealed record SkinTexture(Bitmap Bitmap, int Width, int Height, SkinLayout Layout, bool HasPixels);

public static class SkinTextureLoader
{
    private static readonly HttpClient Http = CreateClient();

    /// <returns><c>null</c> when the address is empty, unreachable or not an image.</returns>
    public static async Task<SkinTexture?> LoadAsync(string? url, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                                           || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        try
        {
            using var response = await Http.GetAsync(uri, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            return Decode(bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    private static SkinTexture Decode(byte[] bytes)
    {
        using var image = Image.Load<Rgba32>(bytes);
        byte Alpha(int x, int y) => x < image.Width && y < image.Height ? image[x, y].A : (byte)0;

        var layout = SkinLayout.Detect(image.Width, image.Height, Alpha);
        var hasPixels = SkinLayout.HasVisiblePixels(image.Width, image.Height, Alpha);
        var bitmap = new Bitmap(new MemoryStream(bytes));

        return new SkinTexture(bitmap, image.Width, image.Height, layout, hasPixels);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"Gml.Launcher-Client-{nameof(GmlClientManager)}/1.0 (OS: {Environment.OSVersion};)");
        return client;
    }
}
