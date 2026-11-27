using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PubPot.Web.Data;
using PubPot.Web.Monitoring;

namespace PubPot.Web.Email;

/// <summary>
/// Arbeitet den Mail-Ausgang ab: fällige Mails holen, verschicken, Ergebnis speichern.
/// Fehlgeschlagene Mails werden später erneut versucht – mit wachsendem Abstand
/// (1, 2, 4, 8 … Minuten, höchstens 1 Stunde), damit ein gestörter Mailserver nicht überrannt wird.
/// </summary>
public sealed class OutboxProcessor(
    AppDbContext db,
    IEmailTransport transport,
    TimeProvider time,
    IOptions<OutboxOptions> options,
    PubPotMetrics metrics,
    ILogger<OutboxProcessor> log)
{
    private const int BatchSize = 20;

    /// <summary>Verschickt alle fälligen Mails (höchstens <see cref="BatchSize"/>). Gibt die Anzahl der Versuche zurück.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var maxAttempts = options.Value.MaxAttempts;

        var due = await db.OutboxEmails
            .Where(m => m.SentAt == null && m.NextAttemptAt <= now && m.Attempts < maxAttempts)
            .OrderBy(m => m.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var email in due)
        {
            email.Attempts++;
            try
            {
                await transport.SendAsync(email, ct);
                email.SentAt = time.GetUtcNow();
                email.LastError = null;
                metrics.MailSent();
                log.LogInformation("Mail {MailId} verschickt (Versuch {Attempt}).", email.Id, email.Attempts);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, email.Attempts - 1)));
                email.NextAttemptAt = time.GetUtcNow() + delay;
                email.LastError = Truncate($"{ex.GetType().Name}: {ex.Message}", 1000);
                metrics.MailFailed();

                // Keine Empfängeradresse im Log (personenbezogen) – die Mail-ID genügt zur Fehlersuche.
                log.LogWarning("Mail {MailId} fehlgeschlagen (Versuch {Attempt}/{Max}): {Error}. Nächster Versuch in {Delay}.",
                    email.Id, email.Attempts, maxAttempts, ex.GetType().Name, delay);
            }

            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}

/// <summary>Hintergrunddienst: ruft den <see cref="OutboxProcessor"/> regelmäßig auf.</summary>
public sealed class OutboxSenderService(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider time,
    ILogger<OutboxSenderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessDueAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // z. B. Datenbank kurz weg – beim nächsten Takt erneut versuchen, den Dienst nicht beenden.
                log.LogError(ex, "Mail-Ausgang konnte nicht abgearbeitet werden.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
