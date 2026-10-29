using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Gml.Client.Interfaces;
using Gml.Client.Presence;
using Sentry;

namespace Gml.Launcher.Core.Services;

public class DiscordPresenceService(
    IGmlClientManager gmlManager,
    ILocalizationService localizationService,
    IBackendChecker backendChecker) : IDiscordPresenceService
{
    private const char PhraseSeparator = '|';
    private readonly Random _random = new();
    private string? _lastPhrase;

    public async Task ShowLauncher()
    {
        if (backendChecker.IsOffline)
            return;

        try
        {
            await gmlManager.UpdateDiscordRpcState(localizationService.GetString(SystemConstants.DefaultDRpcText));
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    public IDisposable TrackGame(LauncherProfileReadDto profile, string playerName)
    {
        if (backendChecker.IsOffline)
            return System.Reactive.Disposables.Disposable.Empty;

        var name = new[] { profile.DiscordName, profile.DisplayName, profile.Name }
            .First(c => !string.IsNullOrWhiteSpace(c))!;
        var details = $"{localizationService.GetString(SystemConstants.PlayDRpcText)} \"{name}\"";

        // No marker yet: vanilla profiles or a modpack without the presence mod only show the profile.
        _ = UpdateGame(profile, details, name, playerName, null);

        return gmlManager.Gameloader.GameOutput
            .Select(line => PresenceLogParser.TryParseDimension(line, out var dimension)
                ? PresenceLogParser.Classify(dimension)
                : (PresenceDimension?)null)
            .Where(dimension => dimension is not null)
            .DistinctUntilChanged()
            .Subscribe(dimension => _ = UpdateGame(profile, details, name, playerName, dimension));
    }

    private async Task UpdateGame(LauncherProfileReadDto profile, string details, string name, string playerName,
        PresenceDimension? dimension)
    {
        try
        {
            var state = dimension is null ? null : PickPhrase(dimension.Value);

            await gmlManager.UpdateDiscordRpcGameState(details, state, profile.DiscordImage, name, playerName);
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    private string? PickPhrase(PresenceDimension dimension)
    {
        var key = dimension switch
        {
            PresenceDimension.Overworld => SystemConstants.PresenceOverworld,
            PresenceDimension.Nether => SystemConstants.PresenceNether,
            PresenceDimension.End => SystemConstants.PresenceEnd,
            PresenceDimension.Other => SystemConstants.PresenceOtherDimension,
            _ => SystemConstants.PresenceMenu
        };

        var phrases = localizationService.GetString(key)
            .Split(PhraseSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (phrases.Length == 0)
            return null;

        // Re-entering a dimension shouldn't show the exact same line again when there's a choice.
        var candidates = phrases.Length > 1 ? phrases.Where(c => c != _lastPhrase).ToArray() : phrases;
        _lastPhrase = candidates[_random.Next(candidates.Length)];

        return _lastPhrase;
    }
}
