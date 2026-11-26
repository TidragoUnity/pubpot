using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PubPot.Web.Email;

namespace PubPot.Web.Data;

/// <summary>Aufbewahrungsfristen (Abschnitt "Cleanup"). Datensparsamkeit: nichts länger behalten als nötig.</summary>
public sealed class CleanupOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Unbestätigte Anmeldungen: gelöscht, wenn die letzte Einreichung so lange her ist.</summary>
    public TimeSpan UnconfirmedRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Verschickte Mails (enthalten E-Mail-Adressen).</summary>
    public TimeSpan SentMailRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Endgültig gescheiterte Mails bleiben etwas länger – zur Fehlersuche.</summary>
    public TimeSpan FailedMailRetention { get; set; } = TimeSpan.FromDays(30);
}

public sealed record CleanupResult(int UnconfirmedSignups, int ExpiredLinks, int SentMails, int FailedMails);

/// <summary>Löscht, was nicht mehr gebraucht wird. Direkt in der Datenbank (ExecuteDelete), ohne Daten zu laden.</summary>
public sealed class DataCleanup(
    AppDbContext db,
    TimeProvider time,
    IOptions<CleanupOptions> options,
    IOptions<OutboxOptions> outbox,
    ILogger<DataCleanup> log)
{
    public async Task<CleanupResult> RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var o = options.Value;

        // "Bestätigst du nicht innerhalb von 30 Tagen, löschen wir deine Angaben automatisch."
        // Gezählt ab der letzten Einreichung (updated_at) – passend zum 30 Tage gültigen Link aus der letzten Mail.
        // Bestätigungslinks hängen per ON DELETE CASCADE dran und verschwinden mit.
        var unconfirmedBefore = now - o.UnconfirmedRetention;
        var signups = await db.Signups
            .Where(s => s.ConfirmedAt == null && s.UpdatedAt < unconfirmedBefore)
            .ExecuteDeleteAsync(ct);

        // Abgelaufene Links (z. B. nicht bestätigte Änderungen an bereits bestätigten Anmeldungen).
        var links = await db.SignupConfirmations.Where(c => c.ExpiresAt < now).ExecuteDeleteAsync(ct);

        var sentBefore = now - o.SentMailRetention;
        var sent = await db.OutboxEmails.Where(m => m.SentAt != null && m.SentAt < sentBefore).ExecuteDeleteAsync(ct);

        var failedBefore = now - o.FailedMailRetention;
        var maxAttempts = outbox.Value.MaxAttempts;
        var failed = await db.OutboxEmails
            .Where(m => m.SentAt == null && m.Attempts >= maxAttempts && m.CreatedAt < failedBefore)
            .ExecuteDeleteAsync(ct);

        var result = new CleanupResult(signups, links, sent, failed);
        if (signups + links + sent + failed > 0)
        {
            log.LogInformation("Aufgeräumt: {Signups} unbestätigte Anmeldungen, {Links} abgelaufene Links, " +
                "{Sent} versendete und {Failed} gescheiterte Mails gelöscht.", signups, links, sent, failed);
        }

        return result;
    }
}

/// <summary>Hintergrunddienst: räumt kurz nach dem Start und dann regelmäßig auf.</summary>
public sealed class DataCleanupService(
    IServiceScopeFactory scopes,
    IOptions<CleanupOptions> options,
    TimeProvider time,
    ILogger<DataCleanupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kurz warten, damit der Start (Migrationen, Healthchecks) nicht mit dem Aufräumen konkurriert.
        await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken);

        using var timer = new PeriodicTimer(options.Value.Interval, time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DataCleanup>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Aufräumen fehlgeschlagen, nächster Versuch beim nächsten Takt.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
