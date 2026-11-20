using Gml.Launcher.Core.Diagnostics;

namespace Gml.Launcher.Tests.Diagnostics;

/// <summary>
///     Before «Играть» the launcher warns when the disk cannot fit the profile download plus a
///     reserve, or when free RAM is below what the game is given. Boundaries are «exactly enough».
/// </summary>
public class PreLaunchCheckTests
{
    private const long Mb = 1024L * 1024;
    private const long Reserve = PreLaunchCheck.DiskReserveBytes;

    [Test]
    public void EstimateDownload_CountsMissingAndSizeMismatchedFilesOnly()
    {
        var files = new (long ExpectedSize, long? LocalSize)[]
        {
            (100, null), // missing
            (200, 150), // partial / outdated
            (300, 300) // already there
        };

        Assert.That(PreLaunchCheck.EstimateDownloadBytes(files), Is.EqualTo(300));
    }

    [Test]
    public void EstimateDownload_IgnoresNegativeSizes()
    {
        Assert.That(PreLaunchCheck.EstimateDownloadBytes([(-5, null)]), Is.EqualTo(0));
    }

    [Test]
    public void Disk_ExactlyDownloadPlusReserve_IsEnough()
    {
        var issues = PreLaunchCheck.Evaluate(freeDiskBytes: 500 * Mb + Reserve, downloadBytes: 500 * Mb,
            freeRamMb: null, selectedRamMb: 4096);

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public void Disk_OneByteShort_Warns_WithNumbers()
    {
        var issues = PreLaunchCheck.Evaluate(freeDiskBytes: 500 * Mb + Reserve - 1, downloadBytes: 500 * Mb,
            freeRamMb: null, selectedRamMb: 4096);

        var issue = issues.Single();
        Assert.Multiple(() =>
        {
            Assert.That(issue.Kind, Is.EqualTo(PreLaunchIssueKind.LowDiskSpace));
            Assert.That(issue.Required, Is.EqualTo(500 * Mb + Reserve));
            Assert.That(issue.Available, Is.EqualTo(500 * Mb + Reserve - 1));
        });
    }

    [Test]
    public void Disk_NothingToDownload_StillNeedsReserve()
    {
        var issues = PreLaunchCheck.Evaluate(freeDiskBytes: Reserve - 1, downloadBytes: 0, freeRamMb: null,
            selectedRamMb: 0);

        Assert.That(issues.Single().Kind, Is.EqualTo(PreLaunchIssueKind.LowDiskSpace));
    }

    [Test]
    public void Ram_FreeEqualToSelected_IsEnough()
    {
        Assert.That(PreLaunchCheck.Evaluate(null, 0, freeRamMb: 4096, selectedRamMb: 4096), Is.Empty);
    }

    [Test]
    public void Ram_OneMbShort_Warns()
    {
        var issue = PreLaunchCheck.Evaluate(null, 0, freeRamMb: 4095, selectedRamMb: 4096).Single();

        Assert.Multiple(() =>
        {
            Assert.That(issue.Kind, Is.EqualTo(PreLaunchIssueKind.LowMemory));
            Assert.That(issue.Required, Is.EqualTo(4096));
            Assert.That(issue.Available, Is.EqualTo(4095));
        });
    }

    [Test]
    public void BothShort_ReportsBoth_DiskFirst()
    {
        var issues = PreLaunchCheck.Evaluate(0, 10 * Mb, freeRamMb: 1000, selectedRamMb: 2048);

        Assert.That(issues.Select(i => i.Kind),
            Is.EqualTo(new[] { PreLaunchIssueKind.LowDiskSpace, PreLaunchIssueKind.LowMemory }));
    }

    [Test]
    public void UnknownMeasurements_AreSkipped()
    {
        Assert.That(PreLaunchCheck.Evaluate(null, 10 * Mb, null, 4096), Is.Empty);
    }

    [Test]
    public void Describe_FillsTemplatesWithReadableNumbers()
    {
        var issues = new[]
        {
            new PreLaunchIssue(PreLaunchIssueKind.LowDiskSpace, 512 * Mb + Reserve, 300 * Mb),
            new PreLaunchIssue(PreLaunchIssueKind.LowMemory, 4096, 2000)
        };

        var text = PreLaunchCheck.Describe(issues, "disk {0} = {1} + {2}, free {3}", "ram free {0} of {1}");

        Assert.That(text, Is.EqualTo("disk 1.5 GB = 512 MB + 1 GB, free 300 MB\n\nram free 2000 of 4096"));
    }
}
