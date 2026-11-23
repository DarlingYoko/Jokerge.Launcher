using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Sentry;

namespace Gml.Launcher.Core.Routes;

/// <summary>Writes the route file for the client mod right before the game starts.</summary>
public sealed class RouteFilePreparer(
    IServerRoutesSource source,
    IMinecraftStatusPinger pinger,
    RouteChoiceStore choices,
    RouteFileWriter? writer = null)
{
    private readonly RouteFileWriter _writer = writer ?? new RouteFileWriter();

    /// <summary>Loads the routes fresh and picks the target: the saved route, or in auto mode the fastest now.</summary>
    public async Task<RouteFilePlan> PlanAsync(string profileName, CancellationToken token = default)
    {
        var routes = await source.GetRoutesAsync(profileName, token).ConfigureAwait(false);
        if (routes is null)
            return RouteFilePlan.Keep;
        if (routes.Count == 0)
            return RouteFilePlan.Remove;

        var saved = RouteSelection.FindSaved(routes, await choices.GetAsync(profileName).ConfigureAwait(false));
        if (saved is not null)
            return RouteFilePlan.Write(routes, saved);
        if (routes.Count == 1)
            return RouteFilePlan.Write(routes, routes[0]);

        var pings = await pinger.PingAllAsync(routes, token).ConfigureAwait(false);
        var pingByRoute = routes.Zip(pings).ToDictionary(pair => pair.First, pair => pair.Second);
        var target = RouteSelection.PickAuto(routes, pingByRoute.GetValueOrDefault);

        return RouteFilePlan.Write(routes, target!);
    }

    /// <summary>
    ///     Writes (or removes) the file now, and again after each pre-launch file sync until disposed: the sync
    ///     deletes files the profile does not list, and this file is never listed.
    /// </summary>
    public async Task<IDisposable> PrepareAsync(string installationDirectory, string profileName,
        IObservable<FileSyncResult> filesSynced, CancellationToken token = default)
    {
        var path = RouteFileWriter.PathFor(installationDirectory, profileName);
        var plan = await PlanAsync(profileName, token).ConfigureAwait(false);
        if (plan.Kind == RouteFilePlanKind.Keep)
            return Disposable.Empty;

        TryApply(path, plan);
        return filesSynced.Subscribe(_ => TryApply(path, plan));
    }

    // The route file only steers the connection; failing to write it must not stop the game from starting.
    private void TryApply(string path, RouteFilePlan plan)
    {
        try
        {
            _writer.Apply(path, plan);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
