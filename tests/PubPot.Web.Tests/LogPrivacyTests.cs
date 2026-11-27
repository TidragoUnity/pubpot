using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using PubPot.Web.Email;
using PubPot.Web.Signups;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Keine personenbezogenen Daten und keine Geheimnisse im Log (Schritt 7.1, E-050). Logs landen später in Docker,
/// in Backups und vielleicht bei einem Dienstleister. Dort haben E-Mail-Adressen, Namen, IPs und Tokens nichts verloren.
/// </summary>
[Collection("Database")]
public sealed partial class LogPrivacyTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    // Auffällige Werte, damit ein Treffer im Log eindeutig aus diesem Test stammt.
    private const string CreatorEmail = "kim.vertraulich@example.com";
    private const string CreatorName = "Kim Vertraulich";
    private const string CreatorChannel = "kim_vertraulich_kanal";
    private const string DeveloperEmail = "dana.vertraulich@example.com";
    private const string DeveloperName = "Dana Vertraulich";
    private const string BotEmail = "bot.vertraulich@example.com";
    private const string ClientIp = "203.0.113.77";
    private const string WrongPassword = "falsches-passwort-vertraulich";
    private const string AdminNote = "Notiz vertraulich";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.Logs.Start();
    }

    public ValueTask DisposeAsync()
    {
        factory.Logs.Stop();
        return ValueTask.CompletedTask;
    }

    private static Dictionary<string, string> CreatorForm(string email = CreatorEmail) => new()
    {
        ["Input.Name"] = CreatorName,
        ["Input.Email"] = email,
        ["Input.Platforms"] = "Twitch",
        ["Input.Channel"] = CreatorChannel,
        ["Input.AudienceSize"] = "Under10",
        ["Input.Genres"] = "Roguelikes",
        ["Input.Adult"] = "true",
        ["Input.Consent"] = "true",
    };

    private static Dictionary<string, string> DeveloperForm() => new()
    {
        ["Input.Name"] = DeveloperName,
        ["Input.Email"] = DeveloperEmail,
        ["Input.Studio"] = "Studio Vertraulich",
        ["Input.GameTitle"] = "Sternenfeld",
        ["Input.SteamLink"] = "https://store.steampowered.com/app/1234560/Sternenfeld/",
        ["Input.Release"] = ReleaseChoice.Released,
        ["Input.HasDemo"] = "true",
        ["Input.NextFestPilot"] = "true",
        ["Input.HasPublisher"] = "false",
        ["Input.Consent"] = "true",
    };

    private static async Task ExpectRedirectAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Whole_flow_logs_no_personal_data_and_no_secrets()
    {
        var client = CreateClient(factory);
        // Wie hinter Caddy: Die IP des Besuchers kommt im Header mit.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ClientIp);

        // Anmeldungen: Creator, Entwickler, dieselbe Adresse noch einmal (Zusammenführen), ein Bot (Honeypot).
        await ExpectRedirectAsync(PostFormAsync(client, "/de/signup/creator", CreatorForm()));
        await ExpectRedirectAsync(PostFormAsync(client, "/en/signup/developer", DeveloperForm()));
        await ExpectRedirectAsync(PostFormAsync(client, "/de/signup/creator", CreatorForm()));
        var bot = CreatorForm(BotEmail);
        bot["Website"] = "https://spam.example";
        await ExpectRedirectAsync(PostFormAsync(client, "/de/signup/creator", bot));

        // Mail-Ausgang: erst fällt der Mailserver aus, dann klappt der Versand.
        factory.Mail.Fail = true;
        await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct));
        factory.Mail.Fail = false;
        await factory.WithDbAsync(db => db.OutboxEmails.ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAt, DateTimeOffset.UnixEpoch), Ct));
        Assert.True(await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct)) > 0);

        // Bestätigen mit dem Token aus der Mail.
        var token = TokenInLink().Match(factory.Mail.Sent.Last(m => m.To == CreatorEmail).TextBody).Groups[1].Value;
        Assert.NotEmpty(token);
        await ExpectRedirectAsync(PostFormAsync(client, "/de/signup/confirm", [new("Token", token)], tokenSource: "/de/signup/creator"));

        // Admin: falsches Passwort, Anmelden, Ansehen, Ändern, Export, Löschen, Abmelden.
        using (var wrong = await PostFormAsync(client, "/admin/login", [new("Username", TestAdmin.Username), new("Password", WrongPassword)]))
        {
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        }

        var cookie = await LoginAsAdminAsync(client);
        var id = await factory.WithDbAsync(db => db.Signups.Where(s => s.Email == CreatorEmail).Select(s => s.Id).SingleAsync(Ct));
        using (var detail = await GetAsync(client, $"/admin/signups/{id}", cookie: cookie))
        {
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        }

        await ExpectRedirectAsync(PostFormAsync(client, $"/admin/signups/{id}",
            [new("Status", "Pilot"), new("NextStepOn", "2026-12-03"), new("Note", AdminNote)], cookie: cookie));
        using (var export = await PostFormAsync(client, "/admin/export", [], tokenSource: "/admin", cookie: cookie))
        {
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        }

        await ExpectRedirectAsync(PostFormAsync(client, $"/admin/signups/{id}/delete", [], cookie: cookie));
        await ExpectRedirectAsync(PostFormAsync(client, "/admin/logout", [], tokenSource: "/admin", cookie: cookie));

        // Hintergrund-Jobs: Aufräumen und Aufbewahrungsfrist.
        await factory.WithServiceAsync<DataCleanup, CleanupResult>(c => c.RunAsync(Ct));
        await factory.WithServiceAsync<SignupRetention, RetentionResult>(r => r.RunAsync(Ct));

        var entries = factory.Logs.Entries;
        // Gegenprobe: Es wurde wirklich mitgeschrieben, sonst wäre der Test wertlos.
        Assert.Contains(entries, e => e.Contains("Admin angemeldet", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Contains("bestätigt", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Contains("fehlgeschlagen", StringComparison.Ordinal));

        string[] forbidden =
        [
            CreatorEmail, DeveloperEmail, BotEmail, CreatorName, DeveloperName, CreatorChannel, "Studio Vertraulich",
            ClientIp, token, TestAdmin.Password, WrongPassword, cookie.Split('=', 2)[1], AdminNote,
        ];
        foreach (var value in forbidden)
        {
            var leak = entries.FirstOrDefault(e => e.Contains(value, StringComparison.OrdinalIgnoreCase));
            Assert.True(leak is null, $"„{value}“ steht im Log: {leak}");
        }
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenInLink();
}
