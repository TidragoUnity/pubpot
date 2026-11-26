using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PubPot.Web.Data;
using PubPot.Web.Signups;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Aufbewahrungsfrist bestätigter Anmeldungen (Schritt 4.9, E-023, E-031): Warnmail nach 12 Monaten ohne Aktivität,
/// „Ich möchte dabei bleiben“, Löschen 30 Tage nach der Warnmail. Was zählt als Aktivität und was nicht.
/// </summary>
[Collection("Database")]
public partial class RetentionTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly HttpClient _client = CreateClient(factory);

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static T Inactive<T>(T signup, int days)
        where T : Signup
    {
        signup.ConfirmedAt = Now.AddDays(-days - 10);
        signup.LastActivityAt = Now.AddDays(-days);
        return signup;
    }

    private Task SeedAsync(params object[] entities) =>
        factory.WithDbAsync(async db =>
        {
            db.AddRange(entities);
            return await db.SaveChangesAsync(Ct);
        });

    private Task<RetentionResult> RunJobAsync() =>
        factory.WithServiceAsync<SignupRetention, RetentionResult>(r => r.RunAsync(Ct));

    private Task<Signup?> LoadAsync(Guid id) =>
        factory.WithDbAsync(db => db.Signups.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, Ct));

    private Task<OutboxEmail> MailToAsync(string to, string subjectStart) =>
        factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(m => m.To == to && m.Subject.StartsWith(subjectStart), Ct));

    private async Task<string> WarnAndGetTokenAsync(Signup signup)
    {
        await SeedAsync(signup);
        Assert.Equal(1, (await RunJobAsync()).Warned);
        var mail = await MailToAsync(signup.Email, signup.Language == "de" ? "Deine Anmeldung bei PubPot wird" : "Your PubPot");
        return KeepToken().Match(mail.TextBody).Groups[1].Value;
    }

    [Fact]
    public async Task Inactive_signups_get_a_warning_mail_in_their_language_once()
    {
        var kim = Inactive(TestData.Creator("kim@example.com", "Kim"), days: 366);
        var alex = Inactive(TestData.Developer("alex@example.com", "Alex"), days: 500);
        await SeedAsync(kim, alex);

        var result = await RunJobAsync();

        Assert.Equal(new RetentionResult(Warned: 2, Deleted: 0), result);
        var de = await MailToAsync("kim@example.com", "Deine Anmeldung bei PubPot wird in 30 Tagen gelöscht");
        Assert.Contains("Hallo Kim,", de.TextBody, StringComparison.Ordinal);
        Assert.Contains("als Creator", de.TextBody, StringComparison.Ordinal);
        Assert.Matches(@"löschen wir deine Anmeldung am \w+, \d{1,2}\. \w+ \d{4}\.", de.TextBody);
        var token = KeepToken().Match(de.TextBody).Groups[1].Value;
        Assert.Contains($"http://localhost/de/signup/keep?token={token}", de.TextBody, StringComparison.Ordinal);

        var en = await MailToAsync("alex@example.com", "Your PubPot sign-up will be deleted in 30 days");
        Assert.Contains("Hi Alex,", en.TextBody, StringComparison.Ordinal);
        Assert.Contains("/en/signup/keep?token=", en.TextBody, StringComparison.Ordinal);

        var stored = await LoadAsync(kim.Id);
        Assert.NotNull(stored!.RetentionWarningSentAt);
        Assert.Equal(ConfirmationToken.Hash(token), stored.RetentionTokenHash); // nur der Hash

        Assert.Equal(new RetentionResult(0, 0), await RunJobAsync()); // keine zweite Warnmail
    }

    [Fact]
    public async Task Active_and_unconfirmed_signups_get_no_warning()
    {
        var active = Inactive(TestData.Creator("aktiv@example.com"), days: 364);
        var unconfirmed = TestData.Developer("offen@example.com", confirmed: false);
        unconfirmed.CreatedAt = unconfirmed.UpdatedAt = Now.AddDays(-400);
        var noActivityYet = TestData.Creator("alt@example.com");
        noActivityYet.ConfirmedAt = Now.AddDays(-400); // ohne LastActivityAt zählt die Bestätigung
        noActivityYet.LastActivityAt = null;
        await SeedAsync(active, unconfirmed, noActivityYet);

        var result = await RunJobAsync();

        Assert.Equal(1, result.Warned);
        Assert.Null((await LoadAsync(active.Id))!.RetentionWarningSentAt);
        Assert.Null((await LoadAsync(unconfirmed.Id))!.RetentionWarningSentAt);
        Assert.NotNull((await LoadAsync(noActivityYet.Id))!.RetentionWarningSentAt);
    }

    [Fact]
    public async Task Signup_is_deleted_30_days_after_the_warning_together_with_its_mails()
    {
        var due = Inactive(TestData.Creator("weg@example.com"), days: 400);
        due.RetentionWarningSentAt = Now.AddDays(-30).AddMinutes(-1);
        var notYet = Inactive(TestData.Developer("bald@example.com"), days: 400);
        notYet.RetentionWarningSentAt = Now.AddDays(-29);
        var mail = new OutboxEmail { To = "weg@example.com", Subject = "x", TextBody = "x", CreatedAt = Now, NextAttemptAt = Now };
        await SeedAsync(due, notYet, mail);

        var result = await RunJobAsync();

        Assert.Equal(new RetentionResult(Warned: 0, Deleted: 1), result);
        Assert.Null(await LoadAsync(due.Id));
        Assert.NotNull(await LoadAsync(notYet.Id));
        Assert.Equal(0, await factory.WithDbAsync(db => db.OutboxEmails.CountAsync(m => m.To == "weg@example.com", Ct)));
    }

    [Fact]
    public async Task Keep_link_shows_a_button_and_only_the_click_restarts_the_period()
    {
        var kim = Inactive(TestData.Creator("kim@example.com", "Kim"), days: 400);
        var token = await WarnAndGetTokenAsync(kim);

        // Nur ansehen (wie ein Mail-Scanner) ändert nichts.
        var prompt = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/de/signup/keep?token={token}"));
        Assert.Contains("Ich möchte dabei bleiben", prompt, StringComparison.Ordinal);
        Assert.NotNull((await LoadAsync(kim.Id))!.RetentionWarningSentAt);

        using var click = await PostFormAsync(_client, "/de/signup/keep", [new("Token", token)], tokenSource: $"/de/signup/keep?token={token}");

        Assert.Equal(HttpStatusCode.Redirect, click.StatusCode);
        Assert.Equal("/de/signup/keep?status=kept", click.Headers.Location?.OriginalString);
        var kept = WebUtility.HtmlDecode(await GetHtmlAsync(_client, "/de/signup/keep?status=kept"));
        Assert.Contains("Danke, deine Anmeldung bleibt bestehen!", kept, StringComparison.Ordinal);

        var stored = (await LoadAsync(kim.Id))!;
        Assert.Null(stored.RetentionWarningSentAt);
        Assert.Null(stored.RetentionTokenHash);
        Assert.True(stored.LastActivityAt > Now.AddMinutes(-5));

        var confirmation = await MailToAsync("kim@example.com", "Deine Anmeldung bei PubPot bleibt bestehen");
        Assert.Contains("Hallo Kim,", confirmation.TextBody, StringComparison.Ordinal);
        Assert.Contains("12 Monate", confirmation.TextBody, StringComparison.Ordinal);

        Assert.Equal(new RetentionResult(0, 0), await RunJobAsync());
    }

    [Fact]
    public async Task Keep_link_works_only_once()
    {
        var token = await WarnAndGetTokenAsync(Inactive(TestData.Developer("alex@example.com"), days: 400));
        using var first = await PostFormAsync(_client, "/en/signup/keep", [new("Token", token)], tokenSource: "/en/signup/creator");

        using var second = await PostFormAsync(_client, "/en/signup/keep", [new("Token", token)], tokenSource: "/en/signup/creator");
        var page = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/en/signup/keep?token={token}"));

        Assert.Equal("/en/signup/keep?status=kept", first.Headers.Location?.OriginalString);
        Assert.Equal("/en/signup/keep?status=invalid", second.Headers.Location?.OriginalString);
        Assert.Contains("This link is invalid.", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?token=zu-kurz")]
    [InlineData("?token=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Unknown_keep_links_are_invalid(string query)
    {
        var html = WebUtility.HtmlDecode(await GetHtmlAsync(_client, "/de/signup/keep" + query));

        Assert.Contains("Dieser Link ist ungültig.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_change_after_the_warning_counts_as_activity_and_prevents_deletion()
    {
        var kim = Inactive(TestData.Creator("kim@example.com"), days: 400);
        await WarnAndGetTokenAsync(kim);
        var cookie = await LoginAsAdminAsync(_client);

        using var save = await PostFormAsync(_client, $"/admin/signups/{kim.Id}", [new("Status", "Pilot"), new("Note", "läuft")], cookie: cookie);

        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var stored = (await LoadAsync(kim.Id))!;
        Assert.Null(stored.RetentionWarningSentAt);
        Assert.Null(stored.RetentionTokenHash);
        Assert.True(stored.LastActivityAt > Now.AddMinutes(-5));
    }

    [Fact]
    public async Task Admin_page_shows_last_activity_and_the_warning()
    {
        var kim = Inactive(TestData.Creator("kim@example.com"), days: 400);
        await WarnAndGetTokenAsync(kim);
        var cookie = await LoginAsAdminAsync(_client);

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/admin/signups/{kim.Id}", cookie: cookie));

        Assert.Contains("Letzte Aktivität", html, StringComparison.Ordinal);
        Assert.Contains("Warnmail", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task First_confirmation_starts_the_period_but_a_resubmitted_form_does_not_restart_it()
    {
        var form = new Dictionary<string, string>
        {
            ["Input.Name"] = "Kim",
            ["Input.Email"] = "kim@example.com",
            ["Input.Platforms"] = "Twitch",
            ["Input.Channel"] = "kim_streamt",
            ["Input.AudienceSize"] = "Under10",
            ["Input.Genres"] = "Roguelikes",
            ["Input.Adult"] = "true",
            ["Input.Consent"] = "true",
        };

        async Task SubmitAndConfirmAsync()
        {
            using var submit = await PostFormAsync(_client, "/de/signup/creator", form);
            var mail = await factory.WithDbAsync(db => db.OutboxEmails.OrderByDescending(m => m.CreatedAt).FirstAsync(Ct));
            var token = ConfirmToken().Match(mail.TextBody).Groups[1].Value;
            using var confirm = await PostFormAsync(_client, "/de/signup/confirm", [new("Token", token)], tokenSource: "/de/signup/creator");
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        }

        await SubmitAndConfirmAsync();
        var first = await factory.WithDbAsync(db => db.Signups.AsNoTracking().SingleAsync(Ct));
        Assert.NotNull(first.LastActivityAt);
        Assert.Equal(first.ConfirmedAt, first.LastActivityAt);

        // Frist künstlich zurückdatieren, dann das Formular erneut abschicken und die Änderung bestätigen.
        var old = Now.AddDays(-200);
        await factory.WithDbAsync(db => db.Signups.ExecuteUpdateAsync(u => u.SetProperty(s => s.LastActivityAt, old), Ct));
        form["Input.Genres"] = "Cozy";
        await SubmitAndConfirmAsync();

        var after = await factory.WithDbAsync(db => db.Signups.AsNoTracking().OfType<CreatorSignup>().SingleAsync(Ct));
        Assert.Equal("Cozy", after.Genres); // Änderung übernommen …
        Assert.Equal(old.ToUnixTimeMilliseconds(), after.LastActivityAt!.Value.ToUnixTimeMilliseconds()); // … Frist aber nicht neu
    }

    [Fact]
    public async Task At_most_the_configured_number_of_warnings_per_run()
    {
        await SeedAsync(
            Inactive(TestData.Creator("a@example.com"), days: 500),
            Inactive(TestData.Creator("b@example.com"), days: 450),
            Inactive(TestData.Creator("c@example.com"), days: 400));
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting("Cleanup:MaxWarningsPerRun", "2"));

        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<SignupRetention>().RunAsync(Ct);

        Assert.Equal(2, result.Warned);
        Assert.Null((await factory.WithDbAsync(db => db.Signups.SingleAsync(s => s.Email == "c@example.com", Ct))).RetentionWarningSentAt);
    }

    /// <summary>Migration für bestehende Daten: letzte Aktivität = später von Bestätigung und Admin-Änderung.</summary>
    [Fact]
    public async Task Migration_fills_last_activity_for_existing_confirmed_signups()
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.ConnectionString);
        var name = "retention_" + Guid.NewGuid().ToString("N")[..8];
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync(Ct);
        }

        builder.Database = name;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString).UseSnakeCaseNamingConvention().Options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("AdminPipeline", Ct);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO signups (id, kind, email, normalized_email, name, language, consent_version, consent_given_at,
                                 created_at, updated_at, confirmed_at, admin_updated_at, channel, genres, pipeline_status)
            VALUES
              ('0193f3c0-0000-7000-8000-000000000001', 'Creator', 'a@example.com', 'a@example.com', 'A', 'de', 't',
               now(), now(), now(), '2026-01-01T00:00:00Z', '2026-06-01T00:00:00Z', 'a', 'x', 'Contacted'),
              ('0193f3c0-0000-7000-8000-000000000002', 'Creator', 'b@example.com', 'b@example.com', 'B', 'de', 't',
               now(), now(), now(), '2026-02-01T00:00:00Z', NULL, 'b', 'x', 'New'),
              ('0193f3c0-0000-7000-8000-000000000003', 'Creator', 'c@example.com', 'c@example.com', 'C', 'de', 't',
               now(), now(), now(), NULL, NULL, 'c', 'x', 'New')
            """, Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);

        var activity = await db.Signups.OrderBy(s => s.Email).Select(s => s.LastActivityAt).ToListAsync(Ct);
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), // Admin-Änderung ist später als die Bestätigung
                new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), // nur Bestätigung
                null, // unbestätigt
            ],
            activity);
    }

    [GeneratedRegex("keep\\?token=([A-Za-z0-9_-]{43})")]
    private static partial Regex KeepToken();

    [GeneratedRegex("confirm\\?token=([A-Za-z0-9_-]{43})")]
    private static partial Regex ConfirmToken();
}
