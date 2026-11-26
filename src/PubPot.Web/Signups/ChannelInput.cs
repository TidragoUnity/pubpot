using System.Text.RegularExpressions;

namespace PubPot.Web.Signups;

/// <summary>
/// "Kanal-Link oder Twitch-Login": entweder ein http(s)-Link mit Domain
/// oder ein Twitch-Benutzername (4–25 Zeichen: Buchstaben, Ziffern, Unterstrich; laut Twitch-Regeln).
/// </summary>
public static partial class ChannelInput
{
    public static bool IsValid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (TwitchLogin().IsMatch(value))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http"
            && uri.Host.Contains('.', StringComparison.Ordinal);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{4,25}$")]
    private static partial Regex TwitchLogin();
}
