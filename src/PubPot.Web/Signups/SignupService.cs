using Microsoft.EntityFrameworkCore;
using Npgsql;
using PubPot.Web.Data;

namespace PubPot.Web.Signups;

/// <summary>
/// Speichert Anmeldungen. Gleiche E-Mail und gleiche Rolle → bestehenden Eintrag aktualisieren
/// statt einen zweiten anzulegen (siehe docs/ENTSCHEIDUNGEN.md, E-020).
/// </summary>
public sealed class SignupService(AppDbContext db, TimeProvider time, ILogger<SignupService> log)
{
    public async Task<Signup> SubmitAsync(SignupSubmission submission, CancellationToken ct)
    {
        try
        {
            return await SaveAsync(submission, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zwei Einreichungen derselben E-Mail gleichzeitig: Die zweite trifft auf den eindeutigen Index.
            // Dann einfach noch einmal – jetzt wird der inzwischen vorhandene Eintrag aktualisiert.
            db.ChangeTracker.Clear();
            log.LogInformation("Gleichzeitige Anmeldung mit derselben E-Mail erkannt, wird zusammengeführt.");
            return await SaveAsync(submission, ct);
        }
    }

    private async Task<Signup> SaveAsync(SignupSubmission submission, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var normalized = Signup.NormalizeEmail(submission.Email);

        var signup = await db.Signups.SingleOrDefaultAsync(
            s => s.Kind == submission.Kind && s.NormalizedEmail == normalized, ct);

        if (signup is null)
        {
            signup = submission.CreateEntity();
            signup.CreatedAt = now;
            db.Signups.Add(signup);
        }

        submission.ApplyTo(signup);
        signup.ConsentVersion = ConsentText.Version;
        signup.ConsentGivenAt = now;
        signup.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        // Bewusst ohne E-Mail oder Namen im Log (keine personenbezogenen Daten in Logs).
        log.LogInformation("Anmeldung {SignupId} ({Kind}) gespeichert.", signup.Id, signup.Kind);
        return signup;
    }
}
