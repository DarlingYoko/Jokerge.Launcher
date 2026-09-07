using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gml.Client.Interfaces;
using Sentry;

namespace Gml.Launcher.Core.Services;

public class FileSyncLogService : IFileSyncLogService
{
    private const string LogFileName = "file-sync.log";

    private readonly IGmlClientManager _manager;

    public FileSyncLogService(IGmlClientManager manager)
    {
        _manager = manager;
    }

    public void LogSync(FileSyncResult result)
    {
        try
        {
            // Read InstallationDirectory fresh each time - it can change after construction
            // (ChangeInstallationFolder), so a cached path here could go stale.
            var logsDirectory = Path.Combine(_manager.InstallationDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            var lines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync check: {result.UpdatedFiles.Count} updated, {result.DeletedFiles.Count} removed"
            };

            if (result.UpdatedFiles.Count > 0)
                lines.Add($"  updated: {string.Join(", ", result.UpdatedFiles.Select(f => f.Directory))}");

            if (result.DeletedFiles.Count > 0)
                lines.Add($"  removed: {string.Join(", ", result.DeletedFiles.Select(f => f.Directory))}");

            File.AppendAllLines(Path.Combine(logsDirectory, LogFileName), lines);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
