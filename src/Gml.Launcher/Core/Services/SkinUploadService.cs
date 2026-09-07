using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Exceptions;
using Sentry;
using Splat;

namespace Gml.Launcher.Core.Services;

public class SkinUploadService : ISkinUploadService
{
    private const string SkinsEndpoint = "api/v1/integrations/texture/skins/load";
    private const string CloaksEndpoint = "api/v1/integrations/texture/cloaks/load";
    private const string ResetSkinEndpoint = "api/v1/integrations/texture/skins/reset";
    private const string ResetCloakEndpoint = "api/v1/integrations/texture/cloaks/reset";

    private readonly IGmlClientManager _manager;

    public SkinUploadService(IGmlClientManager? manager = null)
    {
        _manager = manager ?? Locator.Current.GetService<IGmlClientManager>()
            ?? throw new ServiceNotFoundException(typeof(IGmlClientManager));
    }

    public Task<(bool IsSuccess, string? Error)> UploadSkinAsync(string login, string accessToken, Stream fileStream,
        string fileName, CancellationToken cancellationToken = default)
        => PostTextureAsync(SkinsEndpoint, login, accessToken, fileStream, fileName, cancellationToken);

    public Task<(bool IsSuccess, string? Error)> UploadCloakAsync(string login, string accessToken,
        Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        => PostTextureAsync(CloaksEndpoint, login, accessToken, fileStream, fileName, cancellationToken);

    public Task<(bool IsSuccess, string? Error)> ResetSkinAsync(string login, string accessToken,
        CancellationToken cancellationToken = default)
        => PostResetAsync(ResetSkinEndpoint, login, accessToken, cancellationToken);

    public Task<(bool IsSuccess, string? Error)> ResetCloakAsync(string login, string accessToken,
        CancellationToken cancellationToken = default)
        => PostResetAsync(ResetCloakEndpoint, login, accessToken, cancellationToken);

    private async Task<(bool IsSuccess, string? Error)> PostTextureAsync(
        string relativeEndpoint,
        string login,
        string accessToken,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(_manager.HostUri, relativeEndpoint);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(login), "Login");

            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(streamContent, "Texture", fileName);

            LogRequest($"[SkinUpload] POST {requestUri} login={login} fileName={fileName} size={fileStream.Length}");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            LogRequest($"[SkinUpload] Response {(int)response.StatusCode} {response.StatusCode} from {requestUri}: {body}");

            if (response.IsSuccessStatusCode)
                return (true, null);

            return (false, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }
        catch (Exception exception)
        {
            LogRequest($"[SkinUpload] Request to {requestUri} failed: {exception}");
            SentrySdk.CaptureException(exception);

            return (false, exception.Message);
        }
    }

    private async Task<(bool IsSuccess, string? Error)> PostResetAsync(
        string relativeEndpoint,
        string login,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(_manager.HostUri, relativeEndpoint);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(login), "Login");

            LogRequest($"[SkinReset] POST {requestUri} login={login}");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            LogRequest($"[SkinReset] Response {(int)response.StatusCode} {response.StatusCode} from {requestUri}: {body}");

            if (response.IsSuccessStatusCode)
                return (true, null);

            return (false, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }
        catch (Exception exception)
        {
            LogRequest($"[SkinReset] Request to {requestUri} failed: {exception}");
            SentrySdk.CaptureException(exception);

            return (false, exception.Message);
        }
    }

    private static void LogRequest(string message)
    {
        Debug.WriteLine(message);
        Console.WriteLine(message);
    }
}
