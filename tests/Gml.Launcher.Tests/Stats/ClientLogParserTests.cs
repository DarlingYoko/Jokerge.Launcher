using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class ClientLogParserTests
{
    [Test]
    public void ReadSpan_TakesFirstAndLastTimestamp_IgnoringLinesWithout()
    {
        var span = ClientLogParser.ReadSpan(new[]
        {
            "[15:31:07] [main/INFO]: Loading Minecraft 1.20.1",
            "\tat java.util.concurrent.ForkJoinPool.runWorker(Unknown Source) ~[?:?]",
            "[16:00:00] [Render thread/INFO]: Connecting to waffle.jokerge.com, 25565",
            "[17:09:32] [Thread-1/INFO]: Stopping!",
            "\tat some.stack.Trace(Unknown Source)"
        });

        Assert.That(span, Is.Not.Null);
        Assert.That(span!.Value.FirstTimeOfDay, Is.EqualTo(new TimeSpan(15, 31, 7)));
        Assert.That(span.Value.Duration, Is.EqualTo(new TimeSpan(1, 38, 25)));
    }

    [Test]
    public void ReadSpan_ReturnsNull_WhenNoTimestamps()
    {
        Assert.That(ClientLogParser.ReadSpan(new[] { "no time here", "" }), Is.Null);
    }

    [Test]
    public void ReadSpan_CrossingMidnight_AddsADay()
    {
        var span = ClientLogParser.ReadSpan(new[]
        {
            "[23:50:00] [main/INFO]: start",
            "[23:59:59] [main/INFO]: tick",
            "[00:20:00] [main/INFO]: after midnight"
        });

        Assert.That(span!.Value.Duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [Test]
    public void ReadSpan_SmallBackwardJitter_IsNotTreatedAsMidnight()
    {
        // Async logging can print a line a few seconds "in the past".
        var span = ClientLogParser.ReadSpan(new[]
        {
            "[10:00:00] [main/INFO]: start",
            "[10:47:53] [main/INFO]: a",
            "[10:47:50] [Worker/INFO]: late line",
            "[11:00:00] [main/INFO]: end"
        });

        Assert.That(span!.Value.Duration, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void ReadSpan_LastLineJitter_DoesNotShortenDuration()
    {
        var span = ClientLogParser.ReadSpan(new[]
        {
            "[10:00:00] [main/INFO]: start",
            "[11:00:00] [main/INFO]: a",
            "[10:59:58] [Worker/INFO]: late"
        });

        Assert.That(span!.Value.Duration, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void ReadSpan_UnderstandsForgeTimestampFormat()
    {
        var span = ClientLogParser.ReadSpan(new[]
        {
            "[20Nov2026 12:00:01.123] [main/INFO] [cpw.mods.modlauncher.Launcher/MODLAUNCHER]: start",
            "[20Nov2026 12:30:01.999] [Render thread/INFO] [minecraft/Minecraft]: Stopping!"
        });

        Assert.That(span!.Value.FirstTimeOfDay, Is.EqualTo(new TimeSpan(12, 0, 1)));
        Assert.That(span.Value.Duration, Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [TestCase("2026-10-29-1.log.gz", 2026, 10, 29)]
    [TestCase("2026-11-17-12.log.gz", 2026, 11, 17)]
    public void TryParseArchiveName_ReadsDate(string name, int year, int month, int day)
    {
        Assert.That(ClientLogParser.TryParseArchiveName(name, out var date), Is.True);
        Assert.That(date, Is.EqualTo(new DateOnly(year, month, day)));
    }

    [TestCase("latest.log")]
    [TestCase("debug-1.log.gz")]
    [TestCase("crafttweaker.log")]
    [TestCase("2026-13-45-1.log.gz")]
    public void TryParseArchiveName_RejectsOtherFiles(string name)
    {
        Assert.That(ClientLogParser.TryParseArchiveName(name, out _), Is.False);
    }

    [Test]
    public void ArchivedRun_StartsAtFileDatePlusFirstTimestamp()
    {
        var span = new LogSpan(new TimeSpan(15, 31, 7), TimeSpan.FromMinutes(98));

        var run = span.ToArchivedRun(new DateOnly(2026, 10, 29));

        Assert.That(run.Start, Is.EqualTo(new DateTime(2026, 10, 29, 15, 31, 7)));
        Assert.That(run.Duration, Is.EqualTo(TimeSpan.FromMinutes(98)));
    }

    [Test]
    public void RunEndingAt_StartsDurationBeforeLastWrite()
    {
        var span = new LogSpan(new TimeSpan(17, 7, 24), TimeSpan.FromMinutes(30));

        var run = span.ToRunEndingAt(new DateTime(2026, 11, 19, 19, 51, 46));

        Assert.That(run.Start, Is.EqualTo(new DateTime(2026, 11, 19, 19, 21, 46)));
    }
}
