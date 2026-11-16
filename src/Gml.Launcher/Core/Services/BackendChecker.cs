using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gml.Client;
using Gml.Client.Helpers;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Exceptions;
using Sentry;
using Splat;

namespace Gml.Launcher.Core.Services;

public class BackendChecker: IBackendChecker
{
    private readonly IGmlClientManager _manager;
    public IGmlClientManager Manager => _manager;
    public bool IsOffline => GlobalVariables.BackendInactive;

    public BackendChecker(
        IGmlClientManager? manager=null)
    {
        _manager = manager ?? Locator.Current.GetService<IGmlClientManager>()
            ?? throw new ServiceNotFoundException(typeof(IGmlClientManager));
    }

    /// <summary>
    /// Probes every address in <see cref="GatewayHosts.All"/> at once and switches the client to the
    /// first one in that order that answers, so the direct address wins whenever it works and the
    /// relayed one is used only when the direct one doesn't answer.
    /// </summary>
    public async Task UpdateBackendStatus()
    {
        var reachable = await GatewaySelector.SelectAsync(GatewayHosts.All, Probe, GatewayHosts.ProbeTimeout,
            GatewayHosts.FallbackGrace);

        GlobalVariables.BackendInactive = reachable is null;
        if (reachable is null || GatewaySelector.SameGateway(reachable, _manager.HostUri)) return;

        SentrySdk.AddBreadcrumb($"Backend gateway switched from {_manager.HostUri} to {reachable}");
        _manager.ChangeGateway(reachable);
    }

    public async Task<bool> BackendIsActive()
    {
        try
        {
            return await GmlClientManager.CheckApiAsync(_manager.HostUri.ToString());
        }
        catch (TaskCanceledException exception)
        {
            SentrySdk.CaptureException(exception);
            Console.WriteLine(exception);
            return false;
        }
        catch (HttpRequestException exception)
        {
            SentrySdk.CaptureException(exception);
            Console.WriteLine(exception);
            return false;
        }
    }

    private static async Task<bool> Probe(Uri gateway, CancellationToken cancellationToken)
    {
        try
        {
            return await GmlClientManager.CheckApiAsync(gateway.ToString(), GatewayHosts.ProbeTimeout,
                cancellationToken);
        }
        catch (Exception exception) when (exception is TaskCanceledException or HttpRequestException)
        {
            Console.WriteLine($"Backend {gateway} is not reachable: {exception.Message}");
            return false;
        }
    }
}
