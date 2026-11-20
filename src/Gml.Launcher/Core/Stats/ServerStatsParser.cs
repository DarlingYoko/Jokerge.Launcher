using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gml.Launcher.Core.Stats;

/// <summary>A player's progress on the game server, as the server's stats site counts it.</summary>
/// <param name="PlayTime">Time in the game on the server (the game's own play_time).</param>
/// <param name="Joins">Times the player joined, from the server logs.</param>
/// <param name="LastSeen">When the player was last on the server; <c>null</c> if the logs have no session.</param>
/// <param name="Advancements">Completed advancements, recipes excluded.</param>
/// <param name="AdvancementTotal">Advancements there are to get; 0 if unknown.</param>
public sealed record ServerPlayerStats(
    TimeSpan PlayTime,
    int Joins,
    DateTimeOffset? LastSeen,
    int Advancements,
    int AdvancementTotal);

/// <summary>
///     Reads <c>players.json</c> of the Waffle stats site (server-stats, <c>src/players-json.ts</c>), format version 1.
/// </summary>
public static class ServerStatsParser
{
    public const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     The player by UUID, or else by name (the site keeps one account per name). <c>null</c> if the player
    ///     is not in the file or the file is of another format version.
    /// </summary>
    /// <exception cref="JsonException">The text is not such a file.</exception>
    public static ServerPlayerStats? Find(string json, string playerUuid, string playerName)
    {
        var file = JsonSerializer.Deserialize<PlayersFile>(json, Options)
                   ?? throw new JsonException("players.json is empty");
        if (file.Version != SupportedVersion)
            return null;

        var players = file.Players ?? [];
        var player = players.FirstOrDefault(p => SameUuid(p.Uuid, playerUuid))
                     ?? players.FirstOrDefault(p =>
                         !string.IsNullOrEmpty(playerName) &&
                         string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));

        return player is null
            ? null
            : new ServerPlayerStats(TimeSpan.FromSeconds(player.PlaySeconds), player.Joins, player.LastSeen,
                player.Advancements, file.AdvancementTotal);
    }

    private static bool SameUuid(string? a, string b) =>
        Guid.TryParse(a, out var left) && Guid.TryParse(b, out var right)
            ? left == right
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed class PlayersFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("advancementTotal")] public int AdvancementTotal { get; set; }
        [JsonPropertyName("players")] public List<PlayerEntry>? Players { get; set; }
    }

    private sealed class PlayerEntry
    {
        [JsonPropertyName("uuid")] public string? Uuid { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("playSeconds")] public long PlaySeconds { get; set; }
        [JsonPropertyName("joins")] public int Joins { get; set; }
        [JsonPropertyName("lastSeen")] public DateTimeOffset? LastSeen { get; set; }
        [JsonPropertyName("advancements")] public int Advancements { get; set; }
    }
}
