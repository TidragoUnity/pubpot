using System.Text.RegularExpressions;

namespace PubPot.Web.Signups;

/// <summary>
/// Liest die App-ID aus einem Steam-Shop-Link: store.steampowered.com/app/&lt;ID&gt;.
/// Erlaubt sind http/https, mit oder ohne Protokoll, mit Spielnamen, Abfrage oder Anker dahinter.
/// </summary>
public static partial class SteamLink
{
    public static bool TryParse(string? input, out long appId)
    {
        appId = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = Pattern().Match(input.Trim());
        return match.Success
            && long.TryParse(match.Groups["id"].ValueSpan, out appId)
            && appId > 0;
    }

    [GeneratedRegex(@"^(?:https?://)?store\.steampowered\.com/app/(?<id>\d{1,10})(?:[/?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
