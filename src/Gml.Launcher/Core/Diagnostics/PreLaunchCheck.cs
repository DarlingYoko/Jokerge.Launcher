using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gml.Launcher.Core.Diagnostics;

public enum PreLaunchIssueKind
{
    LowDiskSpace,
    LowMemory
}

/// <param name="Required">Bytes for <see cref="PreLaunchIssueKind.LowDiskSpace" />, MB for memory.</param>
/// <param name="Available">Same unit as <paramref name="Required" />.</param>
public sealed record PreLaunchIssue(PreLaunchIssueKind Kind, long Required, long Available);

/// <summary>
///     Checks before «Играть»: the disk must fit the files still to download plus a reserve for
///     worlds, logs and caches, and free RAM must cover what the game is given.
/// </summary>
public static class PreLaunchCheck
{
    public const long DiskReserveBytes = 1L * 1024 * 1024 * 1024;

    /// <summary>Files that are missing locally or whose size differs will be downloaded again.</summary>
    public static long EstimateDownloadBytes(IEnumerable<(long ExpectedSize, long? LocalSize)> files) =>
        files.Where(f => f.ExpectedSize > 0 && f.LocalSize != f.ExpectedSize).Sum(f => f.ExpectedSize);

    /// <param name="freeDiskBytes">Null when the drive could not be measured; the check is skipped.</param>
    /// <param name="freeRamMb">Null when memory could not be measured; the check is skipped.</param>
    public static IReadOnlyList<PreLaunchIssue> Evaluate(long? freeDiskBytes, long downloadBytes, long? freeRamMb,
        int selectedRamMb)
    {
        var issues = new List<PreLaunchIssue>();

        if (freeDiskBytes is { } freeDisk)
        {
            var required = Math.Max(0, downloadBytes) + DiskReserveBytes;
            if (freeDisk < required)
                issues.Add(new PreLaunchIssue(PreLaunchIssueKind.LowDiskSpace, required, freeDisk));
        }

        if (freeRamMb is { } freeRam && selectedRamMb > 0 && freeRam < selectedRamMb)
            issues.Add(new PreLaunchIssue(PreLaunchIssueKind.LowMemory, selectedRamMb, freeRam));

        return issues;
    }

    /// <summary>
    ///     The warning text: <paramref name="lowDiskTemplate" /> gets needed, download, reserve, free;
    ///     <paramref name="lowMemoryTemplate" /> gets free MB, selected MB. Issues are separated by a blank line.
    /// </summary>
    public static string Describe(IEnumerable<PreLaunchIssue> issues, string lowDiskTemplate,
        string lowMemoryTemplate) =>
        string.Join("\n\n", issues.Select(issue => issue.Kind switch
        {
            PreLaunchIssueKind.LowDiskSpace => string.Format(CultureInfo.InvariantCulture, lowDiskTemplate,
                ByteSize.Format(issue.Required), ByteSize.Format(issue.Required - DiskReserveBytes),
                ByteSize.Format(DiskReserveBytes), ByteSize.Format(issue.Available)),
            PreLaunchIssueKind.LowMemory => string.Format(CultureInfo.InvariantCulture, lowMemoryTemplate,
                issue.Available, issue.Required),
            _ => string.Empty
        }));
}
