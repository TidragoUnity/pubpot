using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;
using PubPot.Web.Data;
using PubPot.Web.Localization;

namespace PubPot.Web.Signups;

public enum ConfirmationStatus
{
    /// <summary>Link ist gültig (nur bei der Vorprüfung) bzw. Anmeldung wurde bestätigt.</summary>
    Valid,
    Invalid,
    Expired,
}

/// <summary>
/// Anmeldungen speichern und per Double-Opt-in bestätigen (siehe docs/ENTSCHEIDUNGEN.md, E-020).
/// <list type="number">
/// <item>Neue E-Mail → neue, unbestätigte Anmeldung.</item>
/// <item>Bekannte, noch unbestätigte E-Mail → Angaben überschreiben (Zusammenführen).</item>
/// <item>Bekannte, bestätigte E-Mail → neue Angaben warten am Bestätigungslink, bis er bestätigt wird.</item>
/// </list>
/// In jedem Fall: neuer Bestätigungslink (ältere verfallen) und eine Mail im Mail-Ausgang – alles in einer Transaktion.
/// </summary>
public sealed class SignupService(
    AppDbContext db,
    TimeProvider time,
    LinkBuilder links,
    IStringLocalizer<SharedResource> text,
    ILogger<SignupService> log)
{
    public async Task SubmitAsync(SignupSubmission submission, CancellationToken ct)
    {
        try
        {
            await SaveAsync(submission, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zwei Einreichungen derselben E-Mail gleichzeitig: Die zweite trifft auf den eindeutigen Index.
            // Dann einfach noch einmal – jetzt wird der inzwischen vorhandene Eintrag gefunden.
            db.ChangeTracker.Clear();
            log.LogInformation("Gleichzeitige Anmeldung mit derselben E-Mail erkannt, wird zusammengeführt.");
            await SaveAsync(submission, ct);
        }
    }

    private async Task SaveAsync(SignupSubmission submission, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var normalized = Signup.NormalizeEmail(submission.Email);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var signup = await db.Signups.SingleOrDefaultAsync(
            s => s.Kind == submission.Kind && s.NormalizedEmail == normalized, ct);

        var isUpdateOfConfirmed = signup is { IsConfirmed: true };
        if (signup is null)
        {
            signup = submission.CreateEntity();
            signup.CreatedAt = now;
            db.Signups.Add(signup);
        }
        else
        {
            // Ältere, noch offene Links für diese Anmeldung verfallen – gültig ist nur der neueste.
            await db.SignupConfirmations.Where(c => c.SignupId == signup.Id).ExecuteDeleteAsync(ct);
        }

        if (!isUpdateOfConfirmed)
        {
            submission.ApplyTo(signup);
            signup.ConsentVersion = ConsentText.Version;
            signup.ConsentGivenAt = now;
            signup.UpdatedAt = now;
        }

        var (token, hash) = ConfirmationToken.Create();
        db.SignupConfirmations.Add(new SignupConfirmation
        {
            Signup = signup,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now + ConfirmationToken.Lifetime,
            PendingChanges = isUpdateOfConfirmed ? PendingChanges.Serialize(submission, now) : null,
        });

        db.OutboxEmails.Add(ConfirmationEmail(submission, token, isUpdateOfConfirmed, now));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Bewusst ohne E-Mail oder Namen im Log (keine personenbezogenen Daten in Logs).
        log.LogInformation("Anmeldung {SignupId} ({Kind}) gespeichert, Bestätigungsmail eingereiht{Pending}.",
            signup.Id, signup.Kind, isUpdateOfConfirmed ? " (Änderung wartet auf Bestätigung)" : "");
    }

    /// <summary>Vorprüfung für die Bestätigungsseite (ändert nichts).</summary>
    public async Task<ConfirmationStatus> CheckAsync(string? token, CancellationToken ct)
    {
        var confirmation = await FindAsync(token, ct);
        return confirmation is null ? ConfirmationStatus.Invalid
            : confirmation.ExpiresAt <= time.GetUtcNow() ? ConfirmationStatus.Expired
            : ConfirmationStatus.Valid;
    }

    /// <summary>Bestätigt die Anmeldung bzw. übernimmt die wartenden Änderungen. Der Link ist danach verbraucht.</summary>
    public async Task<ConfirmationStatus> ConfirmAsync(string? token, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var confirmation = await FindAsync(token, ct);
        if (confirmation is null)
        {
            return ConfirmationStatus.Invalid;
        }

        if (confirmation.ExpiresAt <= now)
        {
            return ConfirmationStatus.Expired;
        }

        var signup = confirmation.Signup;
        if (confirmation.PendingChanges is not null)
        {
            PendingChanges.Apply(signup, confirmation.PendingChanges);
        }

        var firstConfirmation = signup.ConfirmedAt is null;
        signup.ConfirmedAt ??= now;
        signup.UpdatedAt = now;
        db.SignupConfirmations.Remove(confirmation);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Anmeldung {SignupId} bestätigt{Kind}.", signup.Id,
            firstConfirmation ? "" : " (Änderungen übernommen)");
        return ConfirmationStatus.Valid;
    }

    private async Task<SignupConfirmation?> FindAsync(string? token, CancellationToken ct)
    {
        if (!ConfirmationToken.LooksValid(token))
        {
            return null;
        }

        var hash = ConfirmationToken.Hash(token!);
        return await db.SignupConfirmations.Include(c => c.Signup).SingleOrDefaultAsync(c => c.TokenHash == hash, ct);
    }

    /// <summary>Text-Mail in der Sprache der Anmeldung. Bewusst nur Text: kein HTML, das jemand manipulieren könnte.</summary>
    private OutboxEmail ConfirmationEmail(SignupSubmission submission, string token, bool isUpdate, DateTimeOffset now)
    {
        var link = links.Absolute($"/{submission.Language}/signup/confirm?token={token}");

        return CurrentLanguage.InCulture(submission.Language, () =>
        {
            var role = text[submission.Kind == SignupKind.Developer ? "Mail.Role.Developer" : "Mail.Role.Creator"];
            var intro = isUpdate ? text["Mail.Update.Intro"].Value : text["Mail.Confirm.Intro", role].Value;
            var note = isUpdate ? text["Mail.Update.Note"] : text["Mail.Confirm.Note"];

            var body = string.Join("\n",
                text["Mail.Greeting", submission.Name.Trim()],
                "",
                intro,
                "",
                link,
                "",
                note,
                "",
                text["Mail.NotYou"],
                "",
                text["Mail.SignOff"],
                "PubPot");

            return new OutboxEmail
            {
                To = submission.Email.Trim(),
                Subject = isUpdate ? text["Mail.Update.Subject"] : text["Mail.Confirm.Subject"],
                TextBody = body,
                CreatedAt = now,
                NextAttemptAt = now,
            };
        });
    }
}
