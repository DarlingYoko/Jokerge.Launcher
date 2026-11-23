using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Sentry;

namespace Gml.Launcher.Core.Mods;

/// <summary>
///     Before the game starts, turns on the dependencies of every enabled optional mod: now, and again after each
///     file sync (the sync downloads a missing optional mod as disabled).
/// </summary>
public sealed class OptionalModDependencyGuard(
    Func<CancellationToken, Task<IReadOnlyList<OptionalModDetailsDto>?>> loadDetails,
    AutomaticModsStore automaticMods)
{
    public async Task<IDisposable> PrepareAsync(string modsDirectory, string profileName,
        IObservable<FileSyncResult> filesSynced, CancellationToken token = default)
    {
        var details = await loadDetails(token).ConfigureAwait(false);
        if (details is null)
            return Disposable.Empty; // API down: leave the player's files as they are

        IReadOnlyCollection<string> automatic = await automaticMods.GetAsync(profileName).ConfigureAwait(false);

        // Runs on the sync's thread right before the game starts, so the renames stay synchronous.
        void Apply()
        {
            try
            {
                var files = new OptionalModFiles(modsDirectory);
                var graph = OptionalModGraphs.FromDetails(details, files.AllKeys());
                var change = OptionalModToggle.Repair(graph, files.EnabledKeys(), automatic);
                if (change.IsEmpty)
                    return;

                files.Apply(change);
                automatic = change.Automatic;
                _ = automaticMods.SetAsync(profileName, automatic);
            }
            catch (Exception exception)
            {
                SentrySdk.CaptureException(exception);
            }
        }

        Apply();
        return filesSynced.Subscribe(_ => Apply());
    }
}

public static class OptionalModGraphs
{
    /// <summary>Builds the graph from the panel's details, limited to the mods the profile has.</summary>
    public static OptionalModGraph FromDetails(IEnumerable<OptionalModDetailsDto> details, IEnumerable<string> available)
    {
        var dependencies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var mod in details.Where(d => !string.IsNullOrEmpty(d.Key)))
            dependencies[mod.Key] = mod.Dependencies;

        return new OptionalModGraph(available, dependencies);
    }
}
