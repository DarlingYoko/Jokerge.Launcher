using System.Threading.Tasks;
using Gml.Launcher.Core.Services;

namespace Gml.Launcher.Core.Routes;

/// <summary>The route the player picked for each profile; <c>null</c> means «Автоматически» (the default).</summary>
public sealed class RouteChoiceStore(IStorageService storage)
{
    public Task<string?> GetAsync(string profileName) =>
        storage.GetAsync<string>(StorageConstants.SelectedServerRoutePrefix + profileName);

    public Task SetAsync(string profileName, string? routeName) =>
        storage.SetAsync(StorageConstants.SelectedServerRoutePrefix + profileName, routeName);
}
