using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Gml.Launcher.Core.Diagnostics;

/// <summary>
///     Masks secrets and the OS account name before a diagnostics report leaves the PC. Mirrors
///     Jokerge.Api's DiagnosticTextRedactor (profile paths are masked like its SentryMessageNormalizer);
///     the server applies the same rules again.
/// </summary>
public static partial class DiagnosticRedactor
{
    private const string Hidden = "<hidden>";

    [GeneratedRegex(@"(?i)\b([A-Z]:[\\/]Users[\\/])[^\\/'""\r\n]+")]
    private static partial Regex WindowsProfile();

    [GeneratedRegex(@"(/(?:home|Users)/)[^/'""\s]+")]
    private static partial Regex UnixProfile();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)\b(Bearer)\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex Bearer();

    [GeneratedRegex(@"(?i)(--accessToken\s+)\S+")]
    private static partial Regex AccessTokenArgument();

    [GeneratedRegex(
        @"(?i)\b(access[_-]?token|token|password|passwd|pwd|secret|api[_-]?key|session[_-]?id)(""?\s*[:=]\s*""?)[^\s""'&,;]+")]
    private static partial Regex SecretPair();

    /// <param name="knownSecrets">Exact values to hide anywhere (the player's access token).</param>
    /// <param name="accountNames">OS account names, hidden as whole words outside profile paths too.</param>
    public static string Redact(string? text, IEnumerable<string?>? knownSecrets = null,
        IEnumerable<string?>? accountNames = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (knownSecrets is not null)
        {
            foreach (var secret in knownSecrets)
            {
                if (string.IsNullOrEmpty(secret) || secret.Length < 4) continue;
                text = text.Replace(secret, Hidden, StringComparison.OrdinalIgnoreCase);
            }
        }

        text = WindowsProfile().Replace(text, "$1<user>");
        text = UnixProfile().Replace(text, "$1<user>");

        if (accountNames is not null)
        {
            foreach (var name in accountNames)
            {
                if (string.IsNullOrEmpty(name) || name.Length < 3) continue;
                text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_])",
                    "<user>", RegexOptions.IgnoreCase);
            }
        }

        text = Jwt().Replace(text, "<token>");
        text = Bearer().Replace(text, "$1 <token>");
        text = AccessTokenArgument().Replace(text, "$1" + Hidden);
        text = SecretPair().Replace(text, "$1$2" + Hidden);

        return text;
    }
}
