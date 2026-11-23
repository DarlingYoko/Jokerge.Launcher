using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.Routes;

/// <summary>
///     Server List Ping: connect, handshake into the status state, ask for the status, then time a ping/pong.
///     If the server answers the status but not the ping, the connect + status time stands in for the ping.
/// </summary>
public sealed class MinecraftStatusPinger(TimeSpan? timeout = null) : IMinecraftStatusPinger
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>1.20.1; servers answer the status for any version, the number only shows up in their logs.</summary>
    public const int ProtocolVersion = 763;

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<ServerPingResult> PingAsync(string host, int port, CancellationToken token = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_timeout);

        try
        {
            var watch = Stopwatch.StartNew();
            using var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(host, port, deadline.Token).ConfigureAwait(false);
            var stream = client.GetStream();

            await stream.WriteAsync(MinecraftPacket.Handshake(host, port, ProtocolVersion).Concat(
                MinecraftPacket.StatusRequest()).ToArray(), deadline.Token).ConfigureAwait(false);

            var status = await MinecraftPacket.ReadAsync(stream, deadline.Token).ConfigureAwait(false);
            if (status.Id != 0x00)
                return ServerPingResult.Offline;

            var statusMs = (int)watch.ElapsedMilliseconds;
            return ServerPingResult.Online(await TryPingPongAsync(stream, deadline.Token).ConfigureAwait(false)
                                           ?? statusMs);
        }
        catch (Exception exception) when (!token.IsCancellationRequested && IsNetworkFailure(exception))
        {
            return ServerPingResult.Offline;
        }
    }

    /// <returns>The ping/pong round trip, or <c>null</c> when the server does not answer it properly.</returns>
    private static async Task<int?> TryPingPongAsync(NetworkStream stream, CancellationToken token)
    {
        try
        {
            var payload = DateTime.UtcNow.Ticks;
            var watch = Stopwatch.StartNew();
            await stream.WriteAsync(MinecraftPacket.Ping(payload), token).ConfigureAwait(false);
            var pong = await MinecraftPacket.ReadAsync(stream, token).ConfigureAwait(false);
            watch.Stop();

            return pong.Id == 0x01 && pong.Payload.Length == 8 ? (int)watch.ElapsedMilliseconds : null;
        }
        catch (Exception exception) when (IsNetworkFailure(exception))
        {
            return null;
        }
    }

    private static bool IsNetworkFailure(Exception exception) =>
        exception is SocketException or IOException or OperationCanceledException or InvalidDataException
            or ObjectDisposedException;
}
