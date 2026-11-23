using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.Routes;

/// <summary>A packet read off the wire: its id and the bytes after it.</summary>
public sealed record MinecraftPacketData(int Id, byte[] Payload);

/// <summary>
///     The bits of the Minecraft protocol the Server List Ping needs: VarInts, length-prefixed packets and the
///     handshake/status/ping packets (https://minecraft.wiki/w/Java_Edition_protocol/Server_List_Ping).
/// </summary>
public static class MinecraftPacket
{
    // A status answer carries the server icon, so it can be tens of kilobytes; anything near this is garbage.
    private const int MaxPacketLength = 2 * 1024 * 1024;

    public static void WriteVarInt(List<byte> buffer, int value)
    {
        var unsigned = (uint)value;
        while (unsigned >= 0x80)
        {
            buffer.Add((byte)(unsigned | 0x80));
            unsigned >>= 7;
        }

        buffer.Add((byte)unsigned);
    }

    public static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken token)
    {
        var value = 0;
        var single = new byte[1];
        for (var position = 0; position < 35; position += 7)
        {
            await stream.ReadExactlyAsync(single, token).ConfigureAwait(false);
            value |= (single[0] & 0x7F) << position;
            if ((single[0] & 0x80) == 0)
                return value;
        }

        throw new InvalidDataException("VarInt is longer than 5 bytes");
    }

    /// <summary>Handshake asking for the status state (next state 1).</summary>
    public static byte[] Handshake(string host, int port, int protocolVersion)
    {
        var body = new List<byte> { 0x00 };
        WriteVarInt(body, protocolVersion);
        var hostBytes = Encoding.UTF8.GetBytes(host);
        WriteVarInt(body, hostBytes.Length);
        body.AddRange(hostBytes);
        body.Add((byte)(port >> 8));
        body.Add((byte)port);
        WriteVarInt(body, 1);
        return Frame(body);
    }

    public static byte[] StatusRequest() => Frame([0x00]);

    public static byte[] Ping(long payload)
    {
        var body = new byte[9];
        body[0] = 0x01;
        BinaryPrimitives.WriteInt64BigEndian(body.AsSpan(1), payload);
        return Frame([..body]);
    }

    public static async Task<MinecraftPacketData> ReadAsync(Stream stream, CancellationToken token)
    {
        var length = await ReadVarIntAsync(stream, token).ConfigureAwait(false);
        if (length is < 1 or > MaxPacketLength)
            throw new InvalidDataException($"Unexpected packet length {length}");

        var packet = new byte[length];
        await stream.ReadExactlyAsync(packet, token).ConfigureAwait(false);

        var body = new MemoryStream(packet);
        var id = await ReadVarIntAsync(body, token).ConfigureAwait(false);
        return new MinecraftPacketData(id, packet[(int)body.Position..]);
    }

    private static byte[] Frame(List<byte> body)
    {
        var packet = new List<byte>(body.Count + 5);
        WriteVarInt(packet, body.Count);
        packet.AddRange(body);
        return packet.ToArray();
    }
}
