using System.Threading;
using System.Threading.Tasks;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Core.Services;

/// <summary>A player's progress on the server of a profile, from that server's stats site.</summary>
public interface IServerStatsService
{
    /// <returns><c>null</c> when the profile has no stats site, it is unreachable, or the player is not in it.</returns>
    Task<ServerPlayerStats?> GetAsync(string profileName, string playerUuid, string playerName,
        CancellationToken token = default);
}
