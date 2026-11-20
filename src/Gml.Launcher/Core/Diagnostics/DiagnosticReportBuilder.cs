using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>
///     Turns a <see cref="DiagnosticSnapshot" /> into the masked report: structured for the panel and
///     as text for the player to copy. The text is Russian like the panel that shows it.
/// </summary>
public static class DiagnosticReportBuilder
{
    public const int DefaultMaxLogLines = 100;

    public static DiagnosticReport Build(DiagnosticSnapshot snapshot, IReadOnlyCollection<string?> secrets,
        IReadOnlyCollection<string?> accountNames, int maxLogLines = DefaultMaxLogLines)
    {
        string Mask(string? value) => DiagnosticRedactor.Redact(value, secrets, accountNames);
        string? MaskOptional(string? value) => value is null ? null : Mask(value);

        var report = new DiagnosticReport
        {
            LauncherVersion = snapshot.LauncherVersion,
            Os = new DiagnosticOs
            {
                Description = Mask(snapshot.OsDescription),
                Architecture = snapshot.OsArchitecture,
                Type = snapshot.OsType
            },
            Memory = new DiagnosticMemory { TotalMb = snapshot.TotalRamMb, FreeMb = snapshot.FreeRamMb },
            Disk = new DiagnosticDisk
            {
                GameFolder = Mask(snapshot.GameFolder),
                FreeBytes = snapshot.FreeDiskBytes,
                TotalBytes = snapshot.TotalDiskBytes
            },
            Java = new DiagnosticJava
            {
                Found = !string.IsNullOrEmpty(snapshot.JavaPath),
                Path = MaskOptional(snapshot.JavaPath),
                Version = snapshot.JavaVersion
            },
            Connectivity = snapshot.Connectivity.Select(c => new DiagnosticConnectivity
            {
                Name = c.Name,
                Target = Mask(c.Target),
                Reachable = c.Reachable,
                LatencyMs = c.LatencyMs,
                StatusCode = c.StatusCode,
                Error = MaskOptional(c.Error)
            }).ToList(),
            Profile = new DiagnosticProfile
            {
                Name = snapshot.ProfileName,
                IsDynamicRam = snapshot.IsDynamicRam,
                RamMb = snapshot.RamMb,
                RecommendedRamMb = snapshot.RecommendedRamMb,
                FullScreen = snapshot.FullScreen,
                WindowWidth = snapshot.WindowWidth,
                WindowHeight = snapshot.WindowHeight
            },
            LogTail = snapshot.LogLines.TakeLast(Math.Max(0, maxLogLines)).Select(Mask).ToList(),
            CollectedAt = snapshot.CollectedAt
        };

        report.Text = FormatText(snapshot.UserName, report);
        return report;
    }

    private static string FormatText(string userName, DiagnosticReport report)
    {
        var text = new StringBuilder();
        void Line(string value) => text.Append(value).Append('\n');

        Line("Отчёт диагностики Jokerge Launcher");
        Line($"Игрок: {userName}");
        Line($"Собран: {report.CollectedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
        Line($"Лаунчер: {report.LauncherVersion}");
        Line($"ОС: {report.Os.Description} ({report.Os.Architecture})");
        Line($"Память: всего {ByteSize.Format(report.Memory.TotalMb << 20)}, свободно {ByteSize.Format(report.Memory.FreeMb << 20)}");
        Line($"Диск с папкой игры ({report.Disk.GameFolder}): свободно {ByteSize.Format(report.Disk.FreeBytes)} из {ByteSize.Format(report.Disk.TotalBytes)}");
        Line(report.Java.Found
            ? $"Java: {report.Java.Version ?? "версия неизвестна"} — {report.Java.Path}"
            : "Java: не найдена");

        Line("Связь:");
        foreach (var check in report.Connectivity)
            Line("  " + DescribeConnectivity(check));

        var profile = report.Profile;
        Line($"Профиль: {profile.Name ?? "не выбран"}");
        Line(profile.IsDynamicRam
            ? $"RAM: автоматически {profile.RamMb} МБ"
            : $"RAM: вручную {profile.RamMb} МБ" +
              (profile.RecommendedRamMb is { } recommended ? $" (рекомендуется профилем {recommended} МБ)" : string.Empty));
        Line(profile.FullScreen ? "Окно: полный экран" : $"Окно: {profile.WindowWidth}x{profile.WindowHeight}");

        Line($"Последние строки лога ({report.LogTail.Count}):");
        foreach (var logLine in report.LogTail)
            Line("  " + logLine);

        return text.ToString().TrimEnd('\n');
    }

    private static string DescribeConnectivity(DiagnosticConnectivity check)
    {
        var title = check.Name switch
        {
            "api" => "API",
            "files" => "Файловый сервер",
            "ping" => "Ping",
            _ => check.Name
        };

        if (!check.Reachable)
            return $"{title} {check.Target}: НЕДОСТУПЕН" + (check.Error is null ? string.Empty : $" — {check.Error}");

        var details = new List<string> { "OK" };
        if (check.LatencyMs is { } latency) details.Add($"{latency} мс");
        if (check.StatusCode is { } status) details.Add($"HTTP {status}");
        return $"{title} {check.Target}: {string.Join(", ", details)}";
    }
}
