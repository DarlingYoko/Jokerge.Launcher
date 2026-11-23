using System.Collections.Generic;
using System.Threading.Tasks;
using Gml.Launcher.Core.Services;

namespace Gml.Launcher.Core.Mods;

/// <summary>
///     Per profile, the optional mods that are on only because another mod needs them. Kept in the launcher's
///     storage: the game data folder is rewritten by the file sync.
/// </summary>
public sealed class AutomaticModsStore(IStorageService storage)
{
    public async Task<IReadOnlyCollection<string>> GetAsync(string profileName) =>
        await storage.GetAsync<string[]>(StorageConstants.AutomaticOptionalModsPrefix + profileName)
            .ConfigureAwait(false) ?? [];

    public Task SetAsync(string profileName, IEnumerable<string> keys) =>
        storage.SetAsync(StorageConstants.AutomaticOptionalModsPrefix + profileName, new List<string>(keys).ToArray());
}
