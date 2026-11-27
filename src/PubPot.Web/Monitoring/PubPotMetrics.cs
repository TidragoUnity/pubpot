using System.Diagnostics.Metrics;
using PubPot.Web.Data;

namespace PubPot.Web.Monitoring;

/// <summary>
/// Eigene Zähler der Website für Prometheus (Schritt 7.2, E-051). Labels haben nur feste Werte
/// (Art der Anmeldung, Grund, Typ), nie E-Mail, IP oder IDs. Sonst landen personenbezogene Daten in den Metriken
/// und jede neue ID erzeugt eine neue Zeitreihe.
/// </summary>
/// <remarks>
/// Beim Start meldet <see cref="Initialize"/> jede Zeitreihe einmal mit 0 (Schritt 7.10, E-059). Sonst erscheint eine
/// Reihe erst beim ersten Ereignis, gleich mit dem Wert 1, und <c>increase()</c> in Prometheus übersieht dieses erste
/// Ereignis, weil es keinen Wert davor gibt.
/// </remarks>
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

    public void SpamDropped(SpamReason reason) => _spam.Add(1, Reason(reason));

    public void MailSent() => _mailsSent.Add(1);

    public void MailFailed() => _mailsFailed.Add(1);

    /// <summary>Typ aus <see cref="CleanupType"/>. Nullen werden nicht gezählt.</summary>
    public void Deleted(string type, int count)
    {
        if (count > 0)
        {
            _cleanupDeleted.Add(count, Type(type));
        }
    }

    /// <summary>
    /// Alle Zeitreihen mit 0 anlegen. Läuft erst, wenn die App gestartet ist (<see cref="MetricsInitializer"/>):
    /// Vorher hört OpenTelemetry noch nicht zu, und die Nullen gingen verloren.
    /// </summary>
    public void Initialize()
    {
        foreach (var kind in Enum.GetValues<SignupKind>())
        {
            _signups.Add(0, Kind(kind));
            _confirmations.Add(0, Kind(kind));
        }

        foreach (var reason in Enum.GetValues<SpamReason>())
        {
            _spam.Add(0, Reason(reason));
        }

        _mailsSent.Add(0);
        _mailsFailed.Add(0);
        foreach (var type in CleanupType.All)
        {
            _cleanupDeleted.Add(0, Type(type));
        }
    }

    private static KeyValuePair<string, object?> Kind(SignupKind kind) =>
        new("kind", kind == SignupKind.Developer ? "developer" : "creator");

    private static KeyValuePair<string, object?> Reason(SpamReason reason) => new("reason", reason switch
    {
        SpamReason.Honeypot => "honeypot",
        SpamReason.TooFast => "too_fast",
        _ => "other",
    });

    private static KeyValuePair<string, object?> Type(string type) => new("type", type);
}

/// <summary>Feste Werte für das Label <c>type</c> von <c>pubpot.cleanup.deleted</c>.</summary>
public static class CleanupType
{
    public const string UnconfirmedSignups = "unconfirmed_signups";
    public const string ExpiredLinks = "expired_links";
    public const string SentMails = "sent_mails";
    public const string FailedMails = "failed_mails";
    public const string InactiveSignups = "inactive_signups";

    public static readonly IReadOnlyList<string> All =
        [UnconfirmedSignups, ExpiredLinks, SentMails, FailedMails, InactiveSignups];
}

/// <summary>Ruft <see cref="PubPotMetrics.Initialize"/> auf, sobald die App gestartet ist (E-059).</summary>
internal sealed class MetricsInitializer(PubPotMetrics metrics, IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(metrics.Initialize);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public enum SpamReason
{
    Honeypot,
    TooFast,
}
