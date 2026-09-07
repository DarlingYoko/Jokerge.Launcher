using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.Services;

public interface ISkinUploadService
{
    Task<(bool IsSuccess, string? Error)> UploadSkinAsync(string login, string accessToken, Stream fileStream,
        string fileName, CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string? Error)> UploadCloakAsync(string login, string accessToken, Stream fileStream,
        string fileName, CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string? Error)> ResetSkinAsync(string login, string accessToken,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string? Error)> ResetCloakAsync(string login, string accessToken,
        CancellationToken cancellationToken = default);
}
