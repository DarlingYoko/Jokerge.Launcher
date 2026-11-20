using System;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.Stats;

/// <summary>Where the stats site of a profile's server publishes <c>players.json</c>.</summary>
public static partial class ServerStatsUrl
{
    public const string ProfilePlaceholder = "{profile}";

    /// <param name="template">
    ///     Address of <c>players.json</c>; <c>{profile}</c> is replaced by the profile name in lower case, e.g.
    ///     <c>https://{profile}.stats.jokerge.com/players.json</c>. Empty turns the server block off.
    /// </param>
    /// <returns><c>null</c> when there is no usable address for this profile.</returns>
    public static Uri? For(string template, string profileName)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        var address = template.Trim();
        if (address.Contains(ProfilePlaceholder, StringComparison.Ordinal))
        {
            // The name ends up in a host name: only what a DNS label allows.
            if (!HostLabel().IsMatch(profileName))
                return null;
            address = address.Replace(ProfilePlaceholder, profileName.ToLowerInvariant(), StringComparison.Ordinal);
        }

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")]
    private static partial Regex HostLabel();
}
