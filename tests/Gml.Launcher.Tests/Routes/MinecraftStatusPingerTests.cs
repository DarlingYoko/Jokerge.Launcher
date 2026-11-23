using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gml.Launcher.Core.Routes;

namespace Gml.Launcher.Tests.Routes;

/// <summary>A local server that speaks the Server List Ping protocol, or misbehaves on purpose.</summary>
public class MinecraftStatusPingerTests
{
    private const string StatusJson =
        """{"version":{"name":"1.20.1","protocol":763},"players":{"max":20,"online":3},"description":"Waffle"}""";

    private enum Behaviour
    {
        Answer,
        CloseOnPing,
        Silent
    }

    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        public FakeServer(Behaviour behaviour)
        {
            _listener.Start();
            _loop = Task.Run(() => Serve(behaviour));
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public string? Host { get; private set; }
        public int? HandshakePort { get; private set; }
        public int? NextState { get; private set; }

        private async Task Serve(Behaviour behaviour)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var stream = client.GetStream();

                if (behaviour == Behaviour.Silent)
                {
                    await Task.Delay(Timeout.Infinite, _stop.Token);
                    return;
                }

                var handshake = await MinecraftPacket.ReadAsync(stream, _stop.Token);
                ParseHandshake(handshake.Payload);
                await MinecraftPacket.ReadAsync(stream, _stop.Token); // status request

                var json = Encoding.UTF8.GetBytes(StatusJson);
                var body = new List<byte> { 0x00 };
                MinecraftPacket.WriteVarInt(body, json.Length);
                body.AddRange(json);
                var response = new List<byte>();
                MinecraftPacket.WriteVarInt(response, body.Count);
                response.AddRange(body);
                await stream.WriteAsync(response.ToArray(), _stop.Token);

                var ping = await MinecraftPacket.ReadAsync(stream, _stop.Token);
                if (behaviour == Behaviour.CloseOnPing)
                    return;

                var pong = new List<byte>();
                MinecraftPacket.WriteVarInt(pong, ping.Payload.Length + 1);
                pong.Add(0x01);
                pong.AddRange(ping.Payload);
                await stream.WriteAsync(pong.ToArray(), _stop.Token);
                await Task.Delay(Timeout.Infinite, _stop.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
        }

        private void ParseHandshake(byte[] payload)
        {
            var stream = new MemoryStream(payload);
            MinecraftPacket.ReadVarIntAsync(stream, CancellationToken.None).GetAwaiter().GetResult(); // protocol
            var hostLength = MinecraftPacket.ReadVarIntAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
            var host = new byte[hostLength];
            stream.ReadExactly(host);
            Host = Encoding.UTF8.GetString(host);
            var port = new byte[2];
            stream.ReadExactly(port);
            HandshakePort = BinaryPrimitives.ReadUInt16BigEndian(port);
            NextState = MinecraftPacket.ReadVarIntAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _loop;
        }
    }

    [Test]
    public async Task AnsweringServer_IsOnline_WithItsPing()
    {
        await using var server = new FakeServer(Behaviour.Answer);

        var result = await new MinecraftStatusPinger().PingAsync("127.0.0.1", server.Port);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOnline, Is.True);
            Assert.That(result.LatencyMs, Is.Not.Null.And.GreaterThanOrEqualTo(0).And.LessThan(3000));
            Assert.That(server.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(server.HandshakePort, Is.EqualTo(server.Port));
            Assert.That(server.NextState, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ServerWithoutPong_IsStillOnline_FromTheStatusAnswer()
    {
        await using var server = new FakeServer(Behaviour.CloseOnPing);

        var result = await new MinecraftStatusPinger(TimeSpan.FromSeconds(2)).PingAsync("127.0.0.1", server.Port);

        Assert.That(result.IsOnline, Is.True);
        Assert.That(result.LatencyMs, Is.Not.Null);
    }

    [Test]
    public async Task NothingListening_IsOffline()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var result = await new MinecraftStatusPinger().PingAsync("127.0.0.1", port);

        Assert.That(result, Is.EqualTo(ServerPingResult.Offline));
    }

    [Test]
    public async Task SilentServer_IsOffline_AfterTheTimeout()
    {
        await using var server = new FakeServer(Behaviour.Silent);
        var watch = Stopwatch.StartNew();

        var result = await new MinecraftStatusPinger(TimeSpan.FromMilliseconds(400)).PingAsync("127.0.0.1",
            server.Port);

        Assert.That(result, Is.EqualTo(ServerPingResult.Offline));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)));
    }

    [Test]
    public async Task UnknownHost_IsOffline()
    {
        var result = await new MinecraftStatusPinger(TimeSpan.FromSeconds(2)).PingAsync("no-such-host.invalid", 25565);

        Assert.That(result, Is.EqualTo(ServerPingResult.Offline));
    }
}
