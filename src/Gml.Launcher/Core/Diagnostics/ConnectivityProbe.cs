using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>Network checks for the diagnostics report: API, file server and ICMP ping of the gateway.</summary>
public static class ConnectivityProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    // Same host serves files at /api/v1/file/{hash}; an unknown hash answers 404, which still
    // proves the file endpoint (and the storage behind it) responds. 5xx or no answer = unreachable.
    private const string FileProbePath = "api/v1/file/diagnostics-probe";

    public static async Task<ConnectivityResult[]> CheckAllAsync(Uri gateway, CancellationToken cancellationToken)
    {
        return await Task.WhenAll(
            CheckHttpAsync("api", gateway, new Uri(gateway, "health"), status => status < 400, cancellationToken),
            CheckHttpAsync("files", gateway, new Uri(gateway, FileProbePath), status => status < 500,
                cancellationToken),
            PingAsync(gateway));
    }

    private static async Task<ConnectivityResult> CheckHttpAsync(string name, Uri gateway, Uri url,
        Func<int, bool> isReachable, CancellationToken cancellationToken)
    {
        var target = gateway.GetLeftPart(UriPartial.Authority);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;

            return new ConnectivityResult(name, target, isReachable(status), stopwatch.ElapsedMilliseconds, status,
                isReachable(status) ? null : response.ReasonPhrase);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new ConnectivityResult(name, target, false, null, null,
                exception is TaskCanceledException ? $"нет ответа за {Timeout.TotalSeconds:0} с" : exception.Message);
        }
    }

    private static async Task<ConnectivityResult> PingAsync(Uri gateway)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(gateway.Host, (int)Timeout.TotalMilliseconds);

            return reply.Status == IPStatus.Success
                ? new ConnectivityResult("ping", gateway.Host, true, reply.RoundtripTime, null, null)
                : new ConnectivityResult("ping", gateway.Host, false, null, null, reply.Status.ToString());
        }
        catch (Exception exception) when (exception is PingException or InvalidOperationException)
        {
            return new ConnectivityResult("ping", gateway.Host, false, null, null,
                exception.InnerException?.Message ?? exception.Message);
        }
    }
}
