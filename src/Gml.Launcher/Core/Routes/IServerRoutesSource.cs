using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Routes;

/// <summary>Where a profile's routes come from.</summary>
public interface IServerRoutesSource
{
    /// <returns>
    ///     The routes, main address first; an empty list when the profile has no servers; <c>null</c> when they
    ///     could not be loaded (offline mode, API down, unknown profile).
    /// </returns>
    Task<IReadOnlyList<ServerRouteDto>?> GetRoutesAsync(string profileName, CancellationToken token = default);
}
