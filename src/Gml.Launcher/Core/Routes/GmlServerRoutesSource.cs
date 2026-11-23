using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Services;
using Sentry;

namespace Gml.Launcher.Core.Routes;

/// <summary>Routes from the GML API, asked for with the signed-in player's token.</summary>
public sealed class GmlServerRoutesSource(IGmlClientManager manager, string accessToken, IBackendChecker backend)
    : IServerRoutesSource
{
    public async Task<IReadOnlyList<ServerRouteDto>?> GetRoutesAsync(string profileName,
        CancellationToken token = default)
    {
        if (backend.IsOffline)
            return null;

        try
        {
            var response = await manager.GetServerRoutes(profileName, accessToken).ConfigureAwait(false);
            return response.Data;
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            return null;
        }
    }
}
