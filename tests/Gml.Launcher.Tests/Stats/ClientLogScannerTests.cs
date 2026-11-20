using System.IO.Compression;
using System.Text;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class ClientLogScannerTests
{
    private string _logs = null!;

    [SetUp]
    public void SetUp()
    {
        _logs = Path.Combine(Path.GetTempPath(), "gml-launcher-tests", Guid.NewGuid().ToString("N"), "logs");
        Directory.CreateDirectory(_logs);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(Path.GetDirectoryName(_logs)!, true);
    }

    [Test]
    public void Scan_MissingDirectory_ReturnsNoRuns()
    {
        var scanner = new ClientLogScanner(new InMemoryLogSpanCache());

        Assert.That(scanner.Scan(Path.Combine(_logs, "nope")), Is.Empty);
    }

    [Test]
    public void Scan_ReadsArchivesAndLatestLog_IgnoresOtherLogs()
    {
        WriteGz("2026-10-29-1.log.gz", "[15:31:07] [main/INFO]: start", "[17:09:32] [main/INFO]: stop");
        WriteGz("debug-1.log.gz", "[01:00:00] x", "[05:00:00] y");
        File.WriteAllText(Path.Combine(_logs, "crafttweaker.log"), "[01:00:00] x\n[05:00:00] y\n");
        var latest = Path.Combine(_logs, "latest.log");
        File.WriteAllText(latest, "[17:07:24] [main/INFO]: start\n[17:37:24] [main/INFO]: still running\n");
        File.SetLastWriteTime(latest, new DateTime(2026, 11, 19, 17, 37, 30));

        var runs = new ClientLogScanner(new InMemoryLogSpanCache()).Scan(_logs)
            .OrderBy(r => r.Start).ToList();

        Assert.That(runs, Has.Count.EqualTo(2));
        Assert.That(runs[0], Is.EqualTo(new GameRun(new DateTime(2026, 10, 29, 15, 31, 7), new TimeSpan(1, 38, 25))));
        Assert.That(runs[1], Is.EqualTo(new GameRun(new DateTime(2026, 11, 19, 17, 7, 30), TimeSpan.FromMinutes(30))));
    }

    [Test]
    public void Scan_SkipsLogsWithoutTimestamps()
    {
        WriteGz("2026-10-29-1.log.gz", "garbage");

        Assert.That(new ClientLogScanner(new InMemoryLogSpanCache()).Scan(_logs), Is.Empty);
    }

    [Test]
    public void Scan_ReusesCachedArchiveSpans_ButAlwaysRereadsLatestLog()
    {
        WriteGz("2026-10-29-1.log.gz", "[10:00:00] a", "[11:00:00] b");
        File.WriteAllText(Path.Combine(_logs, "latest.log"), "[12:00:00] a\n[12:10:00] b\n");
        var cache = new InMemoryLogSpanCache();
        new ClientLogScanner(cache).Scan(_logs);

        Assert.That(cache.Count, Is.EqualTo(1), "only immutable archives are cached");

        // Corrupt the archive: a cache hit must not reopen it.
        var archive = Path.Combine(_logs, "2026-10-29-1.log.gz");
        var info = new FileInfo(archive);
        var (length, written) = (info.Length, info.LastWriteTimeUtc);
        File.WriteAllBytes(archive, new byte[length]);
        File.SetLastWriteTimeUtc(archive, written);

        var runs = new ClientLogScanner(cache).Scan(_logs);

        Assert.That(runs.Sum(r => r.Duration.TotalMinutes), Is.EqualTo(70));
    }

    [Test]
    public void TotalTime_SumsEveryRunOfTheProfile()
    {
        WriteGz("2026-10-29-1.log.gz", "[10:00:00] a", "[11:00:00] b");
        WriteGz("2026-10-30-1.log.gz", "[23:50:00] a", "[00:20:00] b");
        File.WriteAllText(Path.Combine(_logs, "latest.log"), "[12:00:00] a\n[12:10:00] b\n");

        var total = new ClientLogScanner(new InMemoryLogSpanCache()).TotalTime(_logs);

        Assert.That(total, Is.EqualTo(TimeSpan.FromMinutes(100)));
    }

    [Test]
    public void TotalTime_WithoutLogs_IsZero()
    {
        var total = new ClientLogScanner(new InMemoryLogSpanCache()).TotalTime(Path.Combine(_logs, "nope"));

        Assert.That(total, Is.EqualTo(TimeSpan.Zero));
    }

    private void WriteGz(string name, params string[] lines)
    {
        using var file = File.Create(Path.Combine(_logs, name));
        using var gz = new GZipStream(file, CompressionLevel.Fastest);
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
        gz.Write(bytes);
    }
}
