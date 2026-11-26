using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;

namespace PubPot.Web.Admin;

/// <summary>Filter der Anmeldeliste. Alles optional, <c>null</c> heißt „egal“.</summary>
public sealed record SignupFilter(
    SignupKind? Kind = null,
    bool? Confirmed = null,
    PipelineStatus? Status = null,
    string? Language = null,
    string? Search = null)
{
    public const int SearchMaxLength = 100;

    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);
}

/// <summary>Eine Zeile der Anmeldeliste (nur, was die Tabelle zeigt).</summary>
public sealed record SignupRow(
    Guid Id,
    SignupKind Kind,
    string Name,
    string Email,
    string Language,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    PipelineStatus Status,
    DateOnly? NextStepOn,
    string? Studio,
    string? GameTitle,
    string? Channel);

public sealed record SignupPage(IReadOnlyList<SignupRow> Rows, int Total, int PageNumber, int PageCount);

/// <summary>Die Felder, die im Admin-Bereich änderbar sind.</summary>
public sealed record PipelineUpdate(PipelineStatus Status, string? Note, DateOnly? NextStepOn);

/// <summary>Ergebnis einer Löschung: wie viele Mails aus dem Mail-Ausgang mit entfernt wurden.</summary>
public sealed record DeleteResult(int RemovedMails);

/// <summary>Weitere Anmeldung derselben Person (gleiche E-Mail, andere Rolle).</summary>
public sealed record RelatedSignup(Guid Id, SignupKind Kind);

/// <summary>Abfragen und Änderungen für den Admin-Bereich: Liste, Filter, Suche, Pipeline, Löschen.</summary>
public sealed class AdminSignups(AppDbContext db, SignupDeletion deletion, TimeProvider time, ILogger<AdminSignups> log)
{
    public const int PageSize = 50;

    /// <summary>Gefilterte Anmeldungen, neueste zuerst.</summary>
    public IQueryable<Signup> Query(SignupFilter filter)
    {
        var query = db.Signups.AsNoTracking();

        if (filter.Kind is { } kind)
        {
            query = query.Where(s => s.Kind == kind);
        }

        if (filter.Confirmed is { } confirmed)
        {
            query = confirmed ? query.Where(s => s.ConfirmedAt != null) : query.Where(s => s.ConfirmedAt == null);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(s => s.PipelineStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Language))
        {
            var language = filter.Language.Trim().ToLowerInvariant();
            query = query.Where(s => s.Language == language);
        }

        if (filter.HasSearch)
        {
            // ILIKE = Groß-/Kleinschreibung egal. % und _ aus der Eingabe gelten als normale Zeichen, nicht als Platzhalter.
            var pattern = "%" + EscapeLike(filter.Search!.Trim()) + "%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, pattern, @"\")
                || EF.Functions.ILike(s.Email, pattern, @"\")
                || (s is DeveloperSignup && (EF.Functions.ILike(((DeveloperSignup)s).Studio, pattern, @"\")
                    || EF.Functions.ILike(((DeveloperSignup)s).GameTitle, pattern, @"\")))
                || (s is CreatorSignup && EF.Functions.ILike(((CreatorSignup)s).Channel, pattern, @"\")));
        }

        return query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id);
    }

    public async Task<SignupPage> ListAsync(SignupFilter filter, int pageNumber, CancellationToken ct)
    {
        var query = Query(filter);
        var total = await query.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        pageNumber = Math.Clamp(pageNumber, 1, pageCount);

        var rows = await query
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new SignupRow(
                s.Id,
                s.Kind,
                s.Name,
                s.Email,
                s.Language,
                s.CreatedAt,
                s.ConfirmedAt,
                s.PipelineStatus,
                s.NextStepOn,
                s is DeveloperSignup ? ((DeveloperSignup)s).Studio : null,
                s is DeveloperSignup ? ((DeveloperSignup)s).GameTitle : null,
                s is CreatorSignup ? ((CreatorSignup)s).Channel : null))
            .ToListAsync(ct);

        return new SignupPage(rows, total, pageNumber, pageCount);
    }

    public Task<Signup?> FindAsync(Guid id, CancellationToken ct) =>
        db.Signups.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>Wartet eine geänderte Anmeldung noch auf die Bestätigung per Mail (E-020)?</summary>
    public Task<bool> HasPendingChangesAsync(Guid id, CancellationToken ct) =>
        db.SignupConfirmations.AnyAsync(c => c.SignupId == id && c.PendingChanges != null, ct);

    /// <summary>
    /// Speichert Status, Notiz und nächsten Schritt. Das zählt als Aktivität (E-023): Bei bestätigten Anmeldungen
    /// beginnt die 12-Monats-Frist neu, und eine laufende Warnung vor dem Löschen ist erledigt.
    /// <see cref="Signup.UpdatedAt"/> bleibt unverändert: Das ist die letzte Einreichung der Person selbst, und
    /// danach richtet sich die Löschfrist unbestätigter Anmeldungen.
    /// </summary>
    /// <returns><c>false</c>, wenn es die Anmeldung nicht (mehr) gibt.</returns>
    public async Task<bool> UpdatePipelineAsync(Guid id, PipelineUpdate update, CancellationToken ct)
    {
        var signup = await db.Signups.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (signup is null)
        {
            return false;
        }

        var now = time.GetUtcNow();
        signup.PipelineStatus = update.Status;
        signup.AdminNote = string.IsNullOrWhiteSpace(update.Note) ? null : update.Note.Trim().ReplaceLineEndings("\n");
        signup.NextStepOn = update.NextStepOn;
        signup.AdminUpdatedAt = now;
        if (signup.IsConfirmed)
        {
            signup.RecordActivity(now);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Admin hat Anmeldung {SignupId} geändert (Status {Status}).", id, update.Status);
        return true;
    }

    /// <summary>Andere Anmeldungen mit derselben E-Mail-Adresse (z. B. als Entwickler und als Creator).</summary>
    public async Task<IReadOnlyList<RelatedSignup>> RelatedAsync(Signup signup, CancellationToken ct) =>
        await db.Signups.AsNoTracking()
            .Where(s => s.NormalizedEmail == signup.NormalizedEmail && s.Id != signup.Id)
            .Select(s => new RelatedSignup(s.Id, s.Kind))
            .ToListAsync(ct);

    /// <summary>Löscht eine Anmeldung endgültig (DSGVO-Löschanfrage, E-028), siehe <see cref="SignupDeletion"/>.</summary>
    /// <returns><c>null</c>, wenn es die Anmeldung nicht (mehr) gibt.</returns>
    public async Task<DeleteResult?> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (await deletion.DeleteAsync(id, ct) is not { } removedMails)
        {
            return null;
        }

        // Nur die ID ins Log, keine Adresse.
        log.LogInformation("Admin hat Anmeldung {SignupId} gelöscht, {Mails} Mails aus dem Mail-Ausgang entfernt.", id, removedMails);
        return new DeleteResult(removedMails);
    }

    public static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
