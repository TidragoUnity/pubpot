using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Pipeline-Felder der Anmeldung (Schritt 4.3) und die Migration für bestehende Daten.</summary>
[Collection("Database")]
public class AdminPipelineModelTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task New_signup_starts_as_new_without_note_or_next_step()
    {
        var id = await factory.WithDbAsync(async db =>
        {
            var signup = TestData.Creator("neu@example.com");
            db.Signups.Add(signup);
            await db.SaveChangesAsync(Ct);
            return signup.Id;
        });

        var stored = await factory.WithDbAsync(db => db.Signups.SingleAsync(s => s.Id == id, Ct));
        Assert.Equal(PipelineStatus.New, stored.PipelineStatus);
        Assert.Null(stored.AdminNote);
        Assert.Null(stored.NextStepOn);
        Assert.Null(stored.AdminUpdatedAt);
    }

    [Fact]
    public async Task Pipeline_fields_are_stored_readably()
    {
        var updated = new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
        var id = await factory.WithDbAsync(async db =>
        {
            var signup = TestData.Developer("studio@example.com");
            signup.PipelineStatus = PipelineStatus.Conversation;
            signup.AdminNote = "Demo ansehen, dann Termin.";
            signup.NextStepOn = new DateOnly(2026, 12, 3);
            signup.AdminUpdatedAt = updated;
            db.Signups.Add(signup);
            await db.SaveChangesAsync(Ct);
            return signup.Id;
        });

        var stored = await factory.WithDbAsync(db => db.Signups.SingleAsync(s => s.Id == id, Ct));
        Assert.Equal(PipelineStatus.Conversation, stored.PipelineStatus);
        Assert.Equal("Demo ansehen, dann Termin.", stored.AdminNote);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.NextStepOn);
        Assert.Equal(updated, stored.AdminUpdatedAt);

        // Als Text in der Datenbank, nicht als Zahl: lesbar in Adminer und robust gegen neue Werte dazwischen.
        var raw = await factory.WithDbAsync(db =>
            db.Database.SqlQuery<string>($"SELECT pipeline_status AS \"Value\" FROM signups WHERE id = {id}").SingleAsync(Ct));
        Assert.Equal("Conversation", raw);
    }

    /// <summary>
    /// Die Migration selbst: Eine Anmeldung von vor Phase 4 bekommt den Status "neu".
    /// Dafür eine eigene Datenbank nur bis zur vorigen Migration aufbauen, eine Zeile einfügen, dann weiter migrieren.
    /// </summary>
    [Fact]
    public async Task Existing_signups_become_new_when_the_migration_runs()
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.ConnectionString);
        var name = "pipeline_" + Guid.NewGuid().ToString("N")[..8];
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync(Ct);
        }

        builder.Database = name;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("CleanupIndex", Ct);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO signups (id, kind, email, normalized_email, name, language, consent_version,
                                 consent_given_at, created_at, updated_at, channel, genres)
            VALUES ('0193f3c0-0000-7000-8000-000000000001', 'Creator', 'alt@example.com', 'alt@example.com', 'Alt',
                    'de', 'test', now(), now(), now(), 'alt_kanal', 'Indie')
            """, Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);

        var signup = await db.Signups.SingleAsync(Ct);
        Assert.Equal(PipelineStatus.New, signup.PipelineStatus);
        Assert.Null(signup.AdminUpdatedAt);
    }
}

/// <summary>Beispiel-Anmeldungen für Datenbank-Tests (fiktive Daten).</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    public static CreatorSignup Creator(string email, string name = "Kim Test", bool confirmed = true) => new()
    {
        Email = email,
        NormalizedEmail = Signup.NormalizeEmail(email),
        Name = name,
        Language = "de",
        ConsentVersion = "test",
        ConsentGivenAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
        ConfirmedAt = confirmed ? Now : null,
        Platforms = CreatorPlatforms.Twitch | CreatorPlatforms.YouTube,
        Channel = "kim_streamt",
        AudienceSize = AudienceSize.From25To49,
        Genres = "Roguelikes, Cozy",
        ConfirmedAdult = true,
    };

    public static DeveloperSignup Developer(string email, string name = "Alex Test", bool confirmed = true) => new()
    {
        Email = email,
        NormalizedEmail = Signup.NormalizeEmail(email),
        Name = name,
        Language = "en",
        ConsentVersion = "test",
        ConsentGivenAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
        ConfirmedAt = confirmed ? Now : null,
        Studio = "Pixel Forge",
        GameTitle = "Tiny Dungeon",
        SteamAppId = 480,
        PlannedRelease = new DateOnly(2027, 3, 1),
        HasDemo = true,
        InterestedInNextFestPilot = true,
    };
}
