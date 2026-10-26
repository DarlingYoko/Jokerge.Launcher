using System;
using System.Threading.Tasks;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Services;

public interface IDiscordPresenceService
{
    /// <summary>Shows the "in the launcher" presence from the Discord integration settings.</summary>
    Task ShowLauncher();

    /// <summary>
    ///     Shows the in-game presence for <paramref name="profile"/> and keeps its status in sync with the
    ///     dimension markers the game prints. Dispose the result when the game exits.
    /// </summary>
    IDisposable TrackGame(LauncherProfileReadDto profile);
}
