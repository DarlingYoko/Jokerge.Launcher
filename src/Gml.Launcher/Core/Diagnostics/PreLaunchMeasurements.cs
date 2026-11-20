using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gml.Dto.Profile;
using Gml.Launcher.Core.Services;
using Sentry;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Measures this PC for <see cref="PreLaunchCheck" /> right before the game starts.</summary>
public static class PreLaunchMeasurements
{
    /// <returns>Issues to warn about; empty when everything fits or could not be measured.</returns>
    public static IReadOnlyList<PreLaunchIssue> Evaluate(ProfileReadInfoDto? profile, string installationDirectory,
        ISystemService systemService, int requestedRamMb)
    {
        try
        {
            var downloadBytes = profile is null
                ? 0
                : PreLaunchCheck.EstimateDownloadBytes(profile.Files.Select(file =>
                    (file.Size, LocalFileSize(installationDirectory, file.Directory))));

            return PreLaunchCheck.Evaluate(FreeDiskBytes(installationDirectory), downloadBytes,
                FreeRamMb(systemService), requestedRamMb);
        }
        catch (Exception exception)
        {
            // A failed measurement must never block the game.
            SentrySdk.CaptureException(exception);
            return [];
        }
    }

    private static long? LocalFileSize(string installationDirectory, string relativePath)
    {
        var path = Path.Combine(installationDirectory, relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar));

        var file = new FileInfo(path);
        return file.Exists ? file.Length : null;
    }

    private static long? FreeDiskBytes(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException
                                              or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Free physical RAM in MB, or null when it cannot be measured on this OS.</summary>
    public static long? FreeRamMb(ISystemService systemService)
    {
        try
        {
            return (long)systemService.GetFreeRam();
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
