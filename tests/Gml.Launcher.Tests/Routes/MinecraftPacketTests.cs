using Gml.Launcher.Core.Routes;

namespace Gml.Launcher.Tests.Routes;

public class MinecraftPacketTests
{
    [TestCase(0, new byte[] { 0x00 })]
    [TestCase(1, new byte[] { 0x01 })]
    [TestCase(127, new byte[] { 0x7F })]
    [TestCase(128, new byte[] { 0x80, 0x01 })]
    [TestCase(255, new byte[] { 0xFF, 0x01 })]
    [TestCase(25565, new byte[] { 0xDD, 0xC7, 0x01 })]
    [TestCase(2097151, new byte[] { 0xFF, 0xFF, 0x7F })]
    [TestCase(int.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 })]
    [TestCase(-1, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public async Task VarInt_RoundTrips(int value, byte[] encoded)
    {
        var buffer = new List<byte>();
        MinecraftPacket.WriteVarInt(buffer, value);

        Assert.That(buffer, Is.EqualTo(encoded));
        Assert.That(await MinecraftPacket.ReadVarIntAsync(new MemoryStream(encoded), CancellationToken.None),
            Is.EqualTo(value));
    }

    [Test]
    public void VarInt_TooLong_IsRejected()
    {
        var stream = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x01]);

        Assert.ThrowsAsync<InvalidDataException>(() =>
            MinecraftPacket.ReadVarIntAsync(stream, CancellationToken.None));
    }

    [Test]
    public void Handshake_MatchesTheProtocol()
    {
        var packet = MinecraftPacket.Handshake("localhost", 25565, 763);

        Assert.That(packet, Is.EqualTo(new byte[]
        {
            0x10, // length
            0x00, // packet id
            0xFB, 0x05, // protocol 763
            0x09, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'h', (byte)'o', (byte)'s', (byte)'t',
            0x63, 0xDD, // port, big endian
            0x01 // next state: status
        }));
    }

    [Test]
    public void StatusRequest_IsAnEmptyPacketZero()
    {
        Assert.That(MinecraftPacket.StatusRequest(), Is.EqualTo(new byte[] { 0x01, 0x00 }));
    }

    [Test]
    public void Ping_CarriesTheLongBigEndian()
    {
        Assert.That(MinecraftPacket.Ping(0x0102030405060708), Is.EqualTo(new byte[]
        {
            0x09, 0x01, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08
        }));
    }

    [Test]
    public async Task ReadPacket_SplitsIdAndPayload()
    {
        var stream = new MemoryStream([0x04, 0x00, 0x02, (byte)'{', (byte)'}']);

        var packet = await MinecraftPacket.ReadAsync(stream, CancellationToken.None);

        Assert.That(packet.Id, Is.EqualTo(0));
        Assert.That(packet.Payload, Is.EqualTo(new byte[] { 0x02, (byte)'{', (byte)'}' }));
    }

    [Test]
    public void ReadPacket_HugeLength_IsRejected()
    {
        var buffer = new List<byte>();
        MinecraftPacket.WriteVarInt(buffer, 64 * 1024 * 1024);

        Assert.ThrowsAsync<InvalidDataException>(() =>
            MinecraftPacket.ReadAsync(new MemoryStream(buffer.ToArray()), CancellationToken.None));
    }
}
