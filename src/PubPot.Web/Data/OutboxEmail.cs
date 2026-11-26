namespace PubPot.Web.Data;

/// <summary>
/// Mail-Ausgang ("Outbox"): Mails werden zuerst in der Datenbank gespeichert und dann von einem
/// Hintergrunddienst verschickt – mit Wiederholungen, falls der Mailserver kurz nicht erreichbar ist.
/// Versendete Mails werden nach einigen Tagen gelöscht (sie enthalten E-Mail-Adressen).
/// </summary>
public sealed class OutboxEmail
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string To { get; set; }

    public required string Subject { get; set; }

    public required string TextBody { get; set; }

    public string? HtmlBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Frühester Zeitpunkt für den nächsten Versuch (wächst nach Fehlschlägen).</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Letzte Fehlermeldung (gekürzt), zur Fehlersuche.</summary>
    public string? LastError { get; set; }
}
