namespace PubPot.Web.Notifications;

/// <summary>
/// Hinweise an das Team bei neuen bestätigten Anmeldungen (Abschnitt "Notifications", E-014/E-030).
/// Beides ist optional: Ohne Wert wird über diesen Weg nichts verschickt.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Empfänger der Hinweis-Mail, z. B. das eigene Postfach. In Docker: <c>Notifications__Email</c>.</summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Discord-Webhook-URL (<c>Notifications__DiscordWebhookUrl</c>). Ist ein Geheimnis: Wer sie kennt, kann in den
    /// Kanal schreiben. Deshalb nie ins Repo und nie ins Log. Leer = Discord aus.
    /// </summary>
    public string DiscordWebhookUrl { get; set; } = "";

    public bool EmailEnabled => !string.IsNullOrWhiteSpace(Email);

    public bool DiscordEnabled => !string.IsNullOrWhiteSpace(DiscordWebhookUrl);

    /// <summary>Beim Start prüfen: gültige Adresse bzw. echte Discord-Webhook-URL über HTTPS.</summary>
    public static bool IsValid(NotificationOptions options) =>
        (!options.EmailEnabled || System.Net.Mail.MailAddress.TryCreate(options.Email.Trim(), out _))
        && (!options.DiscordEnabled || IsDiscordWebhook(options.DiscordWebhookUrl));

    private static bool IsDiscordWebhook(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host is "discord.com" or "discordapp.com"
        && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);
}
