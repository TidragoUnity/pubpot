using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Aufbewahrungsfristen: was gelöscht wird – und vor allem, was NICHT gelöscht wird.</summary>
[Collection("Database")]
public class DataCleanupTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorSignup Signup(string email, int updatedDaysAgo, bool confirmed) => new()
    {
        Email = email,
        NormalizedEmail = email,
        Name = "Test",
        Language = "de",
        ConsentVersion = "test",
        ConsentGivenAt = Now,
        CreatedAt = Now.AddDays(-60),
        UpdatedAt = Now.AddDays(-updatedDaysAgo),
        ConfirmedAt = confirmed ? Now.AddDays(-50) : null,
        Channel = "kim_streamt",
        Genres = "Roguelikes",
    };

    private static OutboxEmail Mail(int createdDaysAgo, int? sentDaysAgo, int attempts) => new()
    {
        To = "test@example.com",
        Subject = "Test",
        TextBody = "Test",
        CreatedAt = Now.AddDays(-createdDaysAgo),
        NextAttemptAt = Now,
        SentAt = sentDaysAgo is { } d ? Now.AddDays(-d) : null,
        Attempts = attempts,
    };

    private Task<CleanupResult> RunCleanupAsync() =>
        factory.WithServiceAsync<DataCleanup, CleanupResult>(c => c.RunAsync(Ct));

    [Fact]
    public async Task Unconfirmed_signups_are_deleted_30_days_after_last_submission()
    {
        await factory.WithDbAsync(async db =>
        {
            db.Signups.AddRange(
                Signup("alt-unbestaetigt@example.com", updatedDaysAgo: 31, confirmed: false),   // weg
                Signup("frisch-unbestaetigt@example.com", updatedDaysAgo: 29, confirmed: false), // bleibt (Link noch gültig)
                Signup("alt-bestaetigt@example.com", updatedDaysAgo: 400, confirmed: true));    // bleibt (bestätigt!)
            return await db.SaveChangesAsync(Ct);
        });

        var result = await RunCleanupAsync();

        Assert.Equal(1, result.UnconfirmedSignups);
        var remaining = await factory.WithDbAsync(db => db.Signups.Select(s => s.Email).OrderBy(e => e).ToListAsync(Ct));
        Assert.Equal(["alt-bestaetigt@example.com", "frisch-unbestaetigt@example.com"], remaining);
    }

    [Fact]
    public async Task Deleting_a_signup_removes_its_links_too()
    {
        await factory.WithDbAsync(async db =>
        {
            var signup = Signup("alt@example.com", updatedDaysAgo: 31, confirmed: false);
            db.Signups.Add(signup);
            db.SignupConfirmations.Add(new SignupConfirmation
            {
                Signup = signup,
                TokenHash = new byte[32],
                CreatedAt = Now.AddDays(-31),
                ExpiresAt = Now.AddDays(5), // absichtlich noch "gültig" – verschwindet trotzdem mit der Anmeldung
            });
            return await db.SaveChangesAsync(Ct);
        });

        await RunCleanupAsync();

        Assert.Equal(0, await factory.WithDbAsync(db => db.SignupConfirmations.CountAsync(Ct)));
    }

    [Fact]
    public async Task Expired_links_are_deleted_but_the_confirmed_signup_stays()
    {
        await factory.WithDbAsync(async db =>
        {
            var signup = Signup("bestaetigt@example.com", updatedDaysAgo: 40, confirmed: true);
            db.Signups.Add(signup);
            db.SignupConfirmations.Add(new SignupConfirmation
            {
                Signup = signup,
                TokenHash = new byte[32],
                CreatedAt = Now.AddDays(-31),
                ExpiresAt = Now.AddDays(-1),
                PendingChanges = "{}",
            });
            return await db.SaveChangesAsync(Ct);
        });

        var result = await RunCleanupAsync();

        Assert.Equal(1, result.ExpiredLinks);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Sent_mails_go_after_7_days_failed_after_30_pending_stay()
    {
        await factory.WithDbAsync(async db =>
        {
            db.OutboxEmails.AddRange(
                Mail(createdDaysAgo: 10, sentDaysAgo: 8, attempts: 1),   // weg: versendet vor > 7 Tagen
                Mail(createdDaysAgo: 3, sentDaysAgo: 2, attempts: 1),    // bleibt: versendet vor 2 Tagen
                Mail(createdDaysAgo: 31, sentDaysAgo: null, attempts: 10), // weg: endgültig gescheitert, > 30 Tage
                Mail(createdDaysAgo: 5, sentDaysAgo: null, attempts: 10),  // bleibt: gescheitert, aber jung (Fehlersuche)
                Mail(createdDaysAgo: 40, sentDaysAgo: null, attempts: 3)); // bleibt: wird noch versucht
            return await db.SaveChangesAsync(Ct);
        });

        var result = await RunCleanupAsync();

        Assert.Equal(new CleanupResult(0, 0, 1, 1), result);
        Assert.Equal(3, await factory.WithDbAsync(db => db.OutboxEmails.CountAsync(Ct)));
    }
}
