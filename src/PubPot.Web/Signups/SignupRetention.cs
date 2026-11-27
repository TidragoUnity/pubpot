using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PubPot.Web.Admin;
using PubPot.Web.Data;
using PubPot.Web.Localization;
using PubPot.Web.Monitoring;

namespace PubPot.Web.Signups;

public sealed record RetentionResult(int Warned, int Deleted);

/// <summary>
/// Aufbewahrungsfrist bestätigter Anmeldungen (E-023, E-031):
/// <list type="number">
/// <item>12 Monate ohne Aktivität → Warnmail mit dem Link „Ich möchte dabei bleiben“.</item>
/// <item>Klick und Bestätigung per Button → Frist beginnt neu, Mail „Löschung verhindert“.</item>
/// <item>Kein Klick und keine Änderung im Admin-Bereich → 30 Tage nach der Warnmail gelöscht.</item>
/// </list>
/// Läuft im Aufräum-Dienst mit (stündlich).
/// </summary>
public sealed class SignupRetention(
    AppDbContext db,
    SignupDeletion deletion,
    TimeProvider time,
    LinkBuilder links,
    IStringLocalizer<SharedResource> text,
    IOptions<CleanupOptions> options,
    PubPotMetrics metrics,
    ILogger<SignupRetention> log)
{
    public async Task<RetentionResult> RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var o = options.Value;

        // 1. Löschen: Warnmail ist 30 Tage her, und seitdem gab es keine Aktivität (die hätte die Warnung zurückgesetzt).
        var deleteBefore = now - o.DeletionNotice;
        var expired = await db.Signups
            .Where(s => s.ConfirmedAt != null && s.RetentionWarningSentAt != null && s.RetentionWarningSentAt <= deleteBefore)
            .Select(s => s.Id)
            .ToListAsync(ct);
        var deleted = 0;
        foreach (var id in expired)
        {
            if (await deletion.DeleteAsync(id, ct) is not null)
            {
                deleted++;
            }
        }

        // 2. Warnen: bestätigt, noch nicht gewarnt, letzte Aktivität älter als 12 Monate. Älteste zuerst.
        var inactiveBefore = now - o.InactiveRetention;
        var inactive = await db.Signups
            .Where(s => s.ConfirmedAt != null && s.RetentionWarningSentAt == null
                && (s.LastActivityAt ?? s.ConfirmedAt) <= inactiveBefore)
            .OrderBy(s => s.LastActivityAt ?? s.ConfirmedAt)
            .Take(o.MaxWarningsPerRun)
            .ToListAsync(ct);
        foreach (var signup in inactive)
        {
            var (token, hash) = ConfirmationToken.Create();
            signup.RetentionWarningSentAt = now;
            signup.RetentionTokenHash = hash;
            db.OutboxEmails.Add(WarningEmail(signup, token, now + o.DeletionNotice, now));
        }

        await db.SaveChangesAsync(ct);
        metrics.Deleted("inactive_signups", deleted);

        if (deleted + inactive.Count > 0)
        {
            log.LogInformation("Aufbewahrungsfrist: {Warned} Warnmails eingereiht, {Deleted} inaktive Anmeldungen gelöscht.",
                inactive.Count, deleted);
        }

        return new RetentionResult(inactive.Count, deleted);
    }

    /// <summary>Vorprüfung für die Seite „Ich möchte dabei bleiben“ (ändert nichts).</summary>
    public async Task<bool> CheckAsync(string? token, CancellationToken ct) => await FindAsync(token, ct) is not null;

    /// <summary>„Ich möchte dabei bleiben“: Frist beginnt neu, und es kommt eine Mail, dass es geklappt hat.</summary>
    /// <returns><c>false</c>, wenn der Link ungültig ist (schon benutzt, Anmeldung gelöscht, unvollständig kopiert).</returns>
    public async Task<bool> KeepAsync(string? token, CancellationToken ct)
    {
        var signup = await FindAsync(token, ct);
        if (signup is null)
        {
            return false;
        }

        var now = time.GetUtcNow();
        signup.RecordActivity(now);
        db.OutboxEmails.Add(KeptEmail(signup, now));
        await db.SaveChangesAsync(ct);

        log.LogInformation("Anmeldung {SignupId} bleibt bestehen (Link aus der Warnmail).", signup.Id);
        return true;
    }

    private async Task<Signup?> FindAsync(string? token, CancellationToken ct)
    {
        if (!ConfirmationToken.LooksValid(token))
        {
            return null;
        }

        var hash = ConfirmationToken.Hash(token!);
        return await db.Signups.SingleOrDefaultAsync(s => s.RetentionTokenHash == hash && s.RetentionWarningSentAt != null, ct);
    }

    private OutboxEmail WarningEmail(Signup signup, string token, DateTimeOffset deleteOn, DateTimeOffset now)
    {
        var link = links.Absolute($"/{signup.Language}/signup/keep?token={token}");
        return Mail(signup, now, "Mail.Retention.Subject", () =>
        [
            text["Mail.Retention.Intro", Role(signup), LongDate(deleteOn, signup.Language)],
            "",
            text["Mail.Retention.Action"],
            "",
            link,
            "",
            text["Mail.Retention.Note"],
        ]);
    }

    private OutboxEmail KeptEmail(Signup signup, DateTimeOffset now) =>
        Mail(signup, now, "Mail.Kept.Subject", () => [text["Mail.Kept.Intro", Role(signup)], "", text["Mail.Kept.Note"]]);

    /// <summary>
    /// Text-Mail in der Sprache der Anmeldung, aufgebaut wie die Bestätigungsmail (E-021).
    /// <paramref name="lines"/> ist eine Funktion: Die Texte müssen erst innerhalb von <c>InCulture</c> gelesen werden,
    /// sonst kämen sie im Hintergrund-Job in der Sprache des Servers.
    /// </summary>
    private OutboxEmail Mail(Signup signup, DateTimeOffset now, string subjectKey, Func<string[]> lines) =>
        CurrentLanguage.InCulture(signup.Language, () => new OutboxEmail
        {
            To = signup.Email,
            Subject = text[subjectKey],
            TextBody = string.Join("\n",
                [text["Mail.Greeting", signup.Name.Trim()], "", .. lines(), "", text["Mail.SignOff"], "PubPot"]),
            CreatedAt = now,
            NextAttemptAt = now,
        });

    private string Role(Signup signup) =>
        text[signup.Kind == SignupKind.Developer ? "Mail.Role.Developer" : "Mail.Role.Creator"];

    private static string LongDate(DateTimeOffset value, string language) =>
        AdminText.InBerlin(value).ToString("D", CultureInfo.GetCultureInfo(language));
}
