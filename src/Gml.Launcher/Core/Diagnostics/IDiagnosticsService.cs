using System;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Collects the «Диагностика» report on this PC and sends it to the server.</summary>
public interface IDiagnosticsService
{
    /// <summary>Measures everything and returns the already masked report.</summary>
    Task<DiagnosticReport> CollectAsync(ILauncherUser user, CancellationToken cancellationToken = default);

    Task<DiagnosticsSendResult> SendAsync(ILauncherUser user, DiagnosticReport report,
        CancellationToken cancellationToken = default);
}

public sealed record DiagnosticsSendResult(bool Success, Guid? Id, string? Error);
