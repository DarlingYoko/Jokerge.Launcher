using System;
using Gml.Launcher.Assets;

namespace Gml.Launcher.Core;

/// <summary>
/// The same backend is published under two names: the direct one, and one relayed through the
/// Moscow bridge for players whose route to the direct one is blocked. On every start the launcher
/// probes both at once and uses the direct one whenever it answers.
/// </summary>
public static class GatewayHosts
{
    // Order is preference: earlier wins when several answer.
    public static readonly Uri[] All =
    [
        new(ResourceKeysDictionary.Host),
        new("https://launcher.ru.jokerge.com")
    ];

    // Upper bound for one address to answer at all.
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    // Once the relayed address has answered, how much longer the direct one still gets. A player whose
    // direct route is blocked (packets silently dropped) waits this, not the full ProbeTimeout.
    public static readonly TimeSpan FallbackGrace = TimeSpan.FromSeconds(1.5);
}
