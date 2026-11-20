using Gml.Launcher.Core.Diagnostics;

namespace Gml.Launcher.Tests.Diagnostics;

/// <summary>
///     «Диагностика» in the launcher settings turns what was measured into one report: structured
///     for the panel and as text the player can copy. Nothing secret may end up in either.
/// </summary>
public class DiagnosticReportBuilderTests
{
    private const string Token = "player-access-token-123";

    private static DiagnosticSnapshot Snapshot() => new()
    {
        UserName = "yoko",
        LauncherVersion = "1.0.0.15",
        OsDescription = "Microsoft Windows 10.0.22631",
        OsArchitecture = "X64",
        OsType = "Windows",
        TotalRamMb = 16384,
        FreeRamMb = 4096,
        GameFolder = @"C:\Users\yoko\AppData\Roaming\jokerge",
        FreeDiskBytes = 50L << 30,
        TotalDiskBytes = 500L << 30,
        JavaPath = @"C:\Users\yoko\AppData\Roaming\jokerge\runtime\java-21\bin\javaw.exe",
        JavaVersion = "21.0.3",
        Connectivity =
        [
            new ConnectivityResult("api", "https://launcher.jokerge.com", true, 40, 200, null),
            new ConnectivityResult("files", "https://launcher.jokerge.com", false, null, null, "timeout")
        ],
        ProfileName = "Waffle",
        IsDynamicRam = false,
        RamMb = 6144,
        RecommendedRamMb = 4096,
        FullScreen = false,
        WindowWidth = 900,
        WindowHeight = 600,
        LogLines = ["[INFO] started", $"--accessToken {Token} --version 1.20", @"opened C:\Users\yoko\x.log"],
        CollectedAt = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc)
    };

    private static DiagnosticReport Build(DiagnosticSnapshot? snapshot = null, int maxLogLines = 100) =>
        DiagnosticReportBuilder.Build(snapshot ?? Snapshot(), [Token], ["yoko-pc-account"], maxLogLines);

    [Test]
    public void StructuredReport_CarriesMeasurements()
    {
        var report = Build();

        Assert.Multiple(() =>
        {
            Assert.That(report.LauncherVersion, Is.EqualTo("1.0.0.15"));
            Assert.That(report.Os.Architecture, Is.EqualTo("X64"));
            Assert.That(report.Memory.FreeMb, Is.EqualTo(4096));
            Assert.That(report.Disk.FreeBytes, Is.EqualTo(50L << 30));
            Assert.That(report.Java.Found, Is.True);
            Assert.That(report.Java.Version, Is.EqualTo("21.0.3"));
            Assert.That(report.Connectivity.Select(c => c.Name), Is.EqualTo(new[] { "api", "files" }));
            Assert.That(report.Profile.Name, Is.EqualTo("Waffle"));
            Assert.That(report.Profile.RamMb, Is.EqualTo(6144));
            Assert.That(report.CollectedAt, Is.EqualTo(new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void Text_HasEverySection()
    {
        var text = Build().Text;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Игрок: yoko"));
            Assert.That(text, Does.Contain("Лаунчер: 1.0.0.15"));
            Assert.That(text, Does.Contain("Microsoft Windows 10.0.22631 (X64)"));
            Assert.That(text, Does.Contain("всего 16 GB, свободно 4 GB"));
            Assert.That(text, Does.Contain("свободно 50 GB из 500 GB"));
            Assert.That(text, Does.Contain("Java: 21.0.3"));
            Assert.That(text, Does.Contain("API https://launcher.jokerge.com: OK, 40 мс, HTTP 200"));
            Assert.That(text, Does.Contain("Файловый сервер https://launcher.jokerge.com: НЕДОСТУПЕН — timeout"));
            Assert.That(text, Does.Contain("Профиль: Waffle"));
            Assert.That(text, Does.Contain("RAM: вручную 6144 МБ (рекомендуется профилем 4096 МБ)"));
            Assert.That(text, Does.Contain("Окно: 900x600"));
            Assert.That(text, Does.Contain("[INFO] started"));
        });
    }

    [Test]
    public void DynamicRam_AndFullScreen_AreDescribed()
    {
        var snapshot = Snapshot();
        snapshot.IsDynamicRam = true;
        snapshot.FullScreen = true;

        var text = Build(snapshot).Text;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("RAM: автоматически 6144 МБ"));
            Assert.That(text, Does.Contain("Окно: полный экран"));
        });
    }

    [Test]
    public void MissingJava_IsReported()
    {
        var snapshot = Snapshot();
        snapshot.JavaPath = null;
        snapshot.JavaVersion = null;

        var report = Build(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(report.Java.Found, Is.False);
            Assert.That(report.Text, Does.Contain("Java: не найдена"));
        });
    }

    [Test]
    public void NoSecretsOrAccountName_InTextOrFields()
    {
        var report = Build();
        var everything = string.Join("\n", report.Text, report.Disk.GameFolder, report.Java.Path,
            string.Join("\n", report.LogTail));

        Assert.Multiple(() =>
        {
            Assert.That(everything, Does.Not.Contain(Token));
            Assert.That(everything, Does.Not.Contain(@"Users\yoko"));
            Assert.That(report.Disk.GameFolder, Is.EqualTo(@"C:\Users\<user>\AppData\Roaming\jokerge"));
        });
    }

    [Test]
    public void LogTail_KeepsOnlyNewestLines()
    {
        var snapshot = Snapshot();
        snapshot.LogLines = Enumerable.Range(1, 10).Select(i => "line " + i).ToList();

        var report = Build(snapshot, maxLogLines: 3);

        Assert.That(report.LogTail, Is.EqualTo(new[] { "line 8", "line 9", "line 10" }));
    }

    [TestCase(0L, "0 B")]
    [TestCase(1536L, "1.5 KB")]
    [TestCase(8L * 1024 * 1024 * 1024, "8 GB")]
    public void ByteSize_FormatsBinaryUnits(long bytes, string expected)
    {
        Assert.That(ByteSize.Format(bytes), Is.EqualTo(expected));
    }
}
