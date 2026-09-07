using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Services;

/// <summary>
///     Records, for troubleshooting, which files a pre-launch integrity sync updated or removed.
/// </summary>
public interface IFileSyncLogService
{
    void LogSync(FileSyncResult result);
}
