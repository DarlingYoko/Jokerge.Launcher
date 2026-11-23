namespace Gml.Launcher.Core.Routes;

/// <summary>Whether a Minecraft server answered the status ping, and how fast (round trip, milliseconds).</summary>
public sealed record ServerPingResult(bool IsOnline, int? LatencyMs)
{
    public static ServerPingResult Offline { get; } = new(false, null);

    public static ServerPingResult Online(int latencyMs) => new(true, latencyMs);
}
