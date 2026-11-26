using Microsoft.Extensions.Options;
using PubPot.Web.Admin;
using PubPot.Web.Data;

namespace PubPot.Web.Notifications;

/// <summary>
/// Meldet eine neu bestätigte Anmeldung per Mail (über die Outbox) und optional an Discord (E-030).
/// Die Nachrichten enthalten keine persönlichen Daten: nur Art, Sprache und den Link zur Detailseite im Admin-Bereich.
/// </summary>
public sealed class SignupNotifier(
    AppDbContext db,
    LinkBuilder links,
    IOptions<NotificationOptions> options,
    DiscordQueue discord)
{
    /// <summary>
    /// Legt die Hinweis-Mail in den Mail-Ausgang. Sie wird mit der Bestätigung im selben <c>SaveChanges</c>
    /// gespeichert: Entweder beides klappt oder nichts, eine Bestätigung ohne Hinweis gibt es so nicht.
    /// </summary>
    public void AddEmail(Signup signup, DateTimeOffset now)
    {
        if (!options.Value.EmailEnabled)
        {
            return;
        }

        var kind = AdminText.Label(signup.Kind);
        db.OutboxEmails.Add(new OutboxEmail
        {
            To = options.Value.Email.Trim(),
            Subject = $"PubPot: neue bestätigte Anmeldung ({kind})",
            TextBody = string.Join("\n",
                "Eine neue Anmeldung wurde bestätigt.",
                "",
                $"Art: {kind}",
                $"Sprache: {AdminText.LanguageLabel(signup.Language)}",
                "",
                "Zur Anmeldung im Admin-Bereich:",
                AdminLink(signup),
                "",
                "Name und E-Mail-Adresse stehen bewusst nicht in dieser Mail (Datensparsamkeit)."),
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }

    /// <summary>Nach dem Speichern: Hinweis für Discord einreihen (nur wenn eingerichtet).</summary>
    public void QueueDiscord(Signup signup)
    {
        if (!options.Value.DiscordEnabled)
        {
            return;
        }

        discord.Enqueue($"Neue bestätigte Anmeldung: {AdminText.Label(signup.Kind)} ({signup.Language.ToUpperInvariant()}). {AdminLink(signup)}");
    }

    private string AdminLink(Signup signup) => links.Absolute($"{AdminAuth.PathPrefix}/signups/{signup.Id}");
}
