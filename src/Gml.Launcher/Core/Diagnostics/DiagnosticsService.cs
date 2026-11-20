using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Services;
using Gml.Launcher.Models;
using Sentry;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>
///     Gathers the measurements for «Диагностика» (the I/O half; turning them into a masked report is
///     <see cref="DiagnosticReportBuilder" />) and posts the report to the API.
/// </summary>
public class DiagnosticsService(
    ISystemService systemService,
    IStorageService storageService,
    IGmlClientManager gmlManager,
    IBackendChecker backendChecker,
    LogHandler logHandler) : IDiagnosticsService
{
    private const int FileSyncLogLines = 40;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<DiagnosticReport> CollectAsync(ILauncherUser user, CancellationToken cancellationToken = default)
    {
        var installationDirectory = gmlManager.InstallationDirectory;

        var connectivityTask = ConnectivityProbe.CheckAllAsync(gmlManager.HostUri, cancellationToken);
        var javaTask = Task.Run(() => JavaLocator.Find(installationDirectory, OperatingSystem.IsWindows()),
            cancellationToken);
        var settings = await storageService.GetAsync<SettingsInfo>(StorageConstants.Settings) ?? SettingsInfo.Default;
        var profileName = await storageService.GetAsync<string>(StorageConstants.LastSelectedProfileName);
        var recommendedRam = await FindRecommendedRam(user, profileName);
        var (freeDisk, totalDisk) = MeasureDisk(installationDirectory);
        var maxRam = Measure(() => (long)systemService.GetMaxRam());
        var (javaPath, javaVersion) = await javaTask;

        var snapshot = new DiagnosticSnapshot
        {
            UserName = user.Name,
            LauncherVersion = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version
                ?.ToString() ?? "unknown",
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            OsType = systemService.GetOsType().ToString(),
            TotalRamMb = maxRam,
            FreeRamMb = Measure(() => (long)systemService.GetFreeRam()),
            GameFolder = installationDirectory,
            FreeDiskBytes = freeDisk,
            TotalDiskBytes = totalDisk,
            JavaPath = javaPath,
            JavaVersion = javaVersion,
            Connectivity = await connectivityTask,
            ProfileName = profileName,
            IsDynamicRam = settings.IsDynamicRam,
            RamMb = RamSelection.Plan(settings.IsDynamicRam, settings.RamValue, recommendedRam, maxRam,
                PreLaunchMeasurements.FreeRamMb(systemService)).AllocatedMb,
            RecommendedRamMb = recommendedRam,
            FullScreen = settings.FullScreen,
            WindowWidth = settings.GameWidth,
            WindowHeight = settings.GameHeight,
            LogLines = ReadLogTail(installationDirectory),
            CollectedAt = DateTime.UtcNow
        };

        return DiagnosticReportBuilder.Build(snapshot, [user.AccessToken], [Environment.UserName]);
    }

    public async Task<DiagnosticsSendResult> SendAsync(ILauncherUser user, DiagnosticReport report,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(gmlManager.HostUri, "api/v1/launcher/diagnostics"))
            {
                Content = JsonContent.Create(new DiagnosticReportSubmit { UserName = user.Name, Report = report },
                    options: Json)
            };
            // Raw token, like /profiles/info; the API also accepts "Bearer <token>".
            request.Headers.TryAddWithoutValidation("Authorization", user.AccessToken);

            using var response = await Http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new DiagnosticsSendResult(false, null, $"HTTP {(int)response.StatusCode}");

            var body = await response.Content.ReadFromJsonAsync<CreatedResponse>(Json, cancellationToken);
            return new DiagnosticsSendResult(true, body?.Data?.Id, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or JsonException)
        {
            SentrySdk.CaptureException(exception);
            return new DiagnosticsSendResult(false, null, exception.Message);
        }
    }

    private async Task<int?> FindRecommendedRam(ILauncherUser user, string? profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return null;

        try
        {
            var profiles = backendChecker.IsOffline
                ? await gmlManager.GetOfflineProfiles()
                : await gmlManager.GetProfiles(user.AccessToken);

            return profiles.Data?.FirstOrDefault(p => p.Name == profileName)?.RecommendedRam;
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            return null;
        }
    }

    private static (long Free, long Total) MeasureDisk(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException
                                              or UnauthorizedAccessException)
        {
            return (-1, -1);
        }
    }

    private static long Measure(Func<long> measure)
    {
        try
        {
            return measure();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return -1;
        }
    }

    private List<string> ReadLogTail(string installationDirectory)
    {
        var lines = new List<string>();

        try
        {
            var fileSyncLog = Path.Combine(installationDirectory, "logs", "file-sync.log");
            if (File.Exists(fileSyncLog))
            {
                using var stream = new FileStream(fileSyncLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var tail = new Queue<string>();
                while (reader.ReadLine() is { } line)
                {
                    tail.Enqueue(line);
                    if (tail.Count > FileSyncLogLines) tail.Dequeue();
                }

                lines.Add("--- file-sync.log ---");
                lines.AddRange(tail);
            }
        }
        catch (IOException exception)
        {
            lines.Add($"file-sync.log: {exception.Message}");
        }

        var gameLog = logHandler.RecentLogs.ToArray();
        if (gameLog.Length > 0)
        {
            lines.Add("--- game output ---");
            lines.AddRange(gameLog);
        }

        return lines;
    }

    private sealed class CreatedResponse
    {
        public CreatedData? Data { get; set; }
    }

    private sealed class CreatedData
    {
        public Guid Id { get; set; }
    }
}
