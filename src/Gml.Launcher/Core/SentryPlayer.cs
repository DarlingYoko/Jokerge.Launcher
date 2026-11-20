using Sentry;

namespace Gml.Launcher.Core;

/// <summary>
///     Tags Sentry reports with the signed-in Minecraft nickname (<c>player</c>), so the panel's
///     "Errors" tab shows which player hit a problem. <c>user.username</c> stays the Windows account
///     that <c>SendDefaultPii</c> fills in: the panel shows both.
/// </summary>
public static class SentryPlayer
{
    public const string TagName = "player";

    /// <summary>Sets the tag for the signed-in player, or removes it when nobody is signed in.</summary>
    public static void Set(string? player) => SentrySdk.ConfigureScope(scope => Apply(scope, player));

    public static void Apply(Scope scope, string? player)
    {
        if (string.IsNullOrWhiteSpace(player))
            scope.UnsetTag(TagName);
        else
            scope.SetTag(TagName, player.Trim());
    }
}
