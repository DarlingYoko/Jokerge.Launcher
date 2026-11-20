using System;
using System.Collections.Generic;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Raw, unmasked measurements gathered by <see cref="DiagnosticsCollector" />.</summary>
public sealed class DiagnosticSnapshot
{
    public string UserName { get; set; } = string.Empty;
    public string LauncherVersion { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
    public long TotalRamMb { get; set; }
    public long FreeRamMb { get; set; }
    public string GameFolder { get; set; } = string.Empty;
    public long FreeDiskBytes { get; set; }
    public long TotalDiskBytes { get; set; }
    public string? JavaPath { get; set; }
    public string? JavaVersion { get; set; }
    public IReadOnlyList<ConnectivityResult> Connectivity { get; set; } = [];
    public string? ProfileName { get; set; }
    public bool IsDynamicRam { get; set; }
    public int RamMb { get; set; }
    public int? RecommendedRamMb { get; set; }
    public bool FullScreen { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public IReadOnlyList<string> LogLines { get; set; } = [];
    public DateTime CollectedAt { get; set; }
}

/// <param name="Name"><c>api</c>, <c>files</c> or <c>ping</c>.</param>
public sealed record ConnectivityResult(
    string Name,
    string Target,
    bool Reachable,
    long? LatencyMs,
    int? StatusCode,
    string? Error);

// The classes below are the JSON contract of POST /api/v1/launcher/diagnostics
// (Jokerge.Api: Core/Diagnostics/DiagnosticReportDtos.cs). Keep property names in sync.

public sealed class DiagnosticReportSubmit
{
    public string UserName { get; set; } = string.Empty;
    public DiagnosticReport Report { get; set; } = new();
}

public sealed class DiagnosticReport
{
    public string LauncherVersion { get; set; } = string.Empty;
    public DiagnosticOs Os { get; set; } = new();
    public DiagnosticMemory Memory { get; set; } = new();
    public DiagnosticDisk Disk { get; set; } = new();
    public DiagnosticJava Java { get; set; } = new();
    public List<DiagnosticConnectivity> Connectivity { get; set; } = [];
    public DiagnosticProfile Profile { get; set; } = new();
    public List<string> LogTail { get; set; } = [];
    public DateTime CollectedAt { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class DiagnosticOs
{
    public string Description { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class DiagnosticMemory
{
    public long TotalMb { get; set; }
    public long FreeMb { get; set; }
}

public sealed class DiagnosticDisk
{
    public string GameFolder { get; set; } = string.Empty;
    public long FreeBytes { get; set; }
    public long TotalBytes { get; set; }
}

public sealed class DiagnosticJava
{
    public bool Found { get; set; }
    public string? Path { get; set; }
    public string? Version { get; set; }
}

public sealed class DiagnosticConnectivity
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Reachable { get; set; }
    public long? LatencyMs { get; set; }
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
}

public sealed class DiagnosticProfile
{
    public string? Name { get; set; }
    public bool IsDynamicRam { get; set; }
    public int RamMb { get; set; }
    public int? RecommendedRamMb { get; set; }
    public bool FullScreen { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
}
