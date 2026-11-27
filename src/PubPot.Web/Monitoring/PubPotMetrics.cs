using System.Diagnostics.Metrics;
using PubPot.Web.Data;

namespace PubPot.Web.Monitoring;

/// <summary>
/// Eigene Zähler der Website für Prometheus (Schritt 7.2, E-051). Labels haben nur feste Werte
/// (Art der Anmeldung, Grund, Typ), nie E-Mail, IP oder IDs. Sonst landen personenbezogene Daten in den Metriken
/// und jede neue ID erzeugt eine neue Zeitreihe.
/// </summary>
public sealed class PubPotMetrics
{
    public const string MeterName = "PubPot.Web";

    private readonly Counter<long> _signups;
    private readonly Counter<long> _confirmations;
    private readonly Counter<long> _spam;
    private readonly Counter<long> _mailsSent;
    private readonly Counter<long> _mailsFailed;
    private readonly Counter<long> _cleanupDeleted;

    public PubPotMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _signups = meter.CreateCounter<long>("pubpot.signups", "{signup}",
            "Gespeicherte Anmeldungen (neu oder geändert), nach Art.");
        _confirmations = meter.CreateCounter<long>("pubpot.signups.confirmed", "{signup}",
            "Bestätigte Anmeldungen (Double-Opt-in), nach Art.");
        _spam = meter.CreateCounter<long>("pubpot.signups.spam", "{signup}",
            "Als Spam verworfene Anmeldungen, nach Grund.");
        _mailsSent = meter.CreateCounter<long>("pubpot.mails.sent", "{mail}",
            "Erfolgreich verschickte Mails.");
        _mailsFailed = meter.CreateCounter<long>("pubpot.mails.failed", "{mail}",
            "Fehlgeschlagene Versandversuche (die Mail wird später erneut versucht).");
        _cleanupDeleted = meter.CreateCounter<long>("pubpot.cleanup.deleted", "{item}",
            "Vom Aufräumen gelöschte Einträge, nach Typ.");
    }

    public void SignupSaved(SignupKind kind) => _signups.Add(1, Kind(kind));

    public void SignupConfirmed(SignupKind kind) => _confirmations.Add(1, Kind(kind));

    public void SpamDropped(SpamReason reason) => _spam.Add(1, new KeyValuePair<string, object?>("reason", reason switch
    {
        SpamReason.Honeypot => "honeypot",
        SpamReason.TooFast => "too_fast",
        _ => "other",
    }));

    public void MailSent() => _mailsSent.Add(1);

    public void MailFailed() => _mailsFailed.Add(1);

    /// <summary>Typ als fester Wert, z. B. <c>unconfirmed_signups</c>. Nullen werden nicht gezählt.</summary>
    public void Deleted(string type, int count)
    {
        if (count > 0)
        {
            _cleanupDeleted.Add(count, new KeyValuePair<string, object?>("type", type));
        }
    }

    private static KeyValuePair<string, object?> Kind(SignupKind kind) =>
        new("kind", kind == SignupKind.Developer ? "developer" : "creator");
}

public enum SpamReason
{
    Honeypot,
    TooFast,
}
