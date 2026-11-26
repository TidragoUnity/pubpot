using Microsoft.EntityFrameworkCore;

namespace PubPot.Web.Data;

/// <summary>
/// Löscht eine Anmeldung endgültig, für den Admin-Bereich (DSGVO-Löschanfrage, E-028) und für die
/// Aufbewahrungsfrist (E-031): die Anmeldung, ihre Bestätigungslinks (per ON DELETE CASCADE) und alle Mails an
/// diese Adresse im Mail-Ausgang, sofern es keine weitere Anmeldung mit derselben Adresse gibt. Deren Mails
/// könnten sonst zur anderen Anmeldung gehören.
/// </summary>
public sealed class SignupDeletion(AppDbContext db)
{
    /// <returns>Zahl der mit entfernten Mails, oder <c>null</c>, wenn es die Anmeldung nicht (mehr) gibt.</returns>
    public async Task<int?> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var email = await db.Signups.Where(s => s.Id == id).Select(s => s.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (email is null)
        {
            return null;
        }

        await db.Signups.Where(s => s.Id == id).ExecuteDeleteAsync(ct);

        var removedMails = 0;
        if (!await db.Signups.AnyAsync(s => s.NormalizedEmail == email, ct))
        {
            // Auch schon versendete Mails: Sie enthalten die Adresse und würden sonst noch 7 Tage gespeichert.
            removedMails = await db.OutboxEmails.Where(m => m.To.ToLower() == email).ExecuteDeleteAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return removedMails;
    }
}
