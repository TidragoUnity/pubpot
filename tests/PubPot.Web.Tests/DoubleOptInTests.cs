using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using PubPot.Web.Email;
using PubPot.Web.Signups;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Double-Opt-in: Mail im Ausgang, Bestätigungslink, Änderungen bestätigter Anmeldungen, Versand.</summary>
[Collection("Database")]
public partial class DoubleOptInTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Dictionary<string, string> CreatorForm(string email = "kim@example.com", string genres = "Roguelikes") => new()
    {
        ["Input.Name"] = "Kim",
        ["Input.Email"] = email,
        ["Input.Platforms"] = "Twitch",
        ["Input.Channel"] = "kim_streamt",
        ["Input.AudienceSize"] = "Under10",
        ["Input.Genres"] = genres,
        ["Input.Adult"] = "true",
        ["Input.Consent"] = "true",
    };

    private async Task<string> SignUpAndGetTokenAsync(string culture = "de", string genres = "Roguelikes")
    {
        using var response = await PostFormAsync(_client, $"/{culture}/signup/creator", CreatorForm(genres: genres));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var mail = await factory.WithDbAsync(db => db.OutboxEmails.OrderByDescending(m => m.CreatedAt).FirstAsync(Ct));
        return TokenInLink().Match(mail.TextBody).Groups[1].Value;
    }

    private async Task<HttpResponseMessage> ConfirmAsync(string culture, string token) =>
        await PostFormAsync(_client, $"/{culture}/signup/confirm", [new("Token", token)], tokenSource: $"/{culture}/signup/creator");

    [Fact]
    public async Task Signup_queues_confirmation_mail_and_stores_only_token_hash()
    {
        var token = await SignUpAndGetTokenAsync();

        var mail = await factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(Ct));
        Assert.Equal("kim@example.com", mail.To);
        Assert.Equal("Bitte bestätige deine Anmeldung bei PubPot", mail.Subject);
        Assert.Contains("Hallo Kim,", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("als Creator", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains($"http://localhost/de/signup/confirm?token={token}", mail.TextBody, StringComparison.Ordinal);
        Assert.Null(mail.SentAt);

        var confirmation = await factory.WithDbAsync(db => db.SignupConfirmations.SingleAsync(Ct));
        Assert.Equal(ConfirmationToken.Hash(token), confirmation.TokenHash);
        Assert.DoesNotContain(token, Convert.ToBase64String(confirmation.TokenHash), StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromDays(30), confirmation.ExpiresAt - confirmation.CreatedAt);
    }

    [Fact]
    public async Task English_signup_gets_english_mail()
    {
        await SignUpAndGetTokenAsync("en");

        var mail = await factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(Ct));
        Assert.Equal("Please confirm your PubPot sign-up", mail.Subject);
        Assert.Contains("as a creator", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("http://localhost/en/signup/confirm?token=", mail.TextBody, StringComparison.Ordinal);
    }

    /// <summary>Wichtig: Das bloße Öffnen des Links (wie es Mail-Scanner tun) bestätigt NICHT.</summary>
    [Fact]
    public async Task Opening_the_link_only_shows_a_button()
    {
        var token = await SignUpAndGetTokenAsync();

        var html = await GetHtmlAsync(_client, $"/de/signup/confirm?token={token}");

        Assert.Contains("Anmeldung bestätigen</button>", html, StringComparison.Ordinal);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
        Assert.Null(await factory.WithDbAsync(db => db.Signups.Select(s => s.ConfirmedAt).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Clicking_the_button_confirms_and_uses_up_the_link()
    {
        var token = await SignUpAndGetTokenAsync();

        using var response = await ConfirmAsync("de", token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/de/signup/confirm?status=confirmed", response.Headers.Location?.OriginalString);
        Assert.NotNull(await factory.WithDbAsync(db => db.Signups.Select(s => s.ConfirmedAt).SingleAsync(Ct)));
        Assert.Equal(0, await factory.WithDbAsync(db => db.SignupConfirmations.CountAsync(Ct)));

        // Ein zweites Mal geht nicht.
        using var again = await ConfirmAsync("de", token);
        Assert.Equal("/de/signup/confirm?status=invalid", again.Headers.Location?.OriginalString);
        Assert.Contains("Danke, deine Anmeldung ist bestätigt!", await GetHtmlAsync(_client, "/de/signup/confirm?status=confirmed"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kurz")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // richtige Form, aber unbekannt
    public async Task Unknown_tokens_are_invalid(string token)
    {
        var html = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/de/signup/confirm?token={token}"));

        Assert.Contains("Dieser Link ist ungültig.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Anmeldung bestätigen</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_links_cannot_confirm()
    {
        var token = await SignUpAndGetTokenAsync();
        await factory.WithDbAsync(db => db.SignupConfirmations.ExecuteUpdateAsync(
            c => c.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));

        Assert.Contains("Dieser Link ist abgelaufen.", await GetHtmlAsync(_client, $"/de/signup/confirm?token={token}"), StringComparison.Ordinal);
        using var response = await ConfirmAsync("de", token);
        Assert.Equal("/de/signup/confirm?status=expired", response.Headers.Location?.OriginalString);
        Assert.Null(await factory.WithDbAsync(db => db.Signups.Select(s => s.ConfirmedAt).SingleAsync(Ct)));
    }

    [Fact]
    public async Task New_signup_invalidates_older_links()
    {
        var first = await SignUpAndGetTokenAsync();
        var second = await SignUpAndGetTokenAsync();

        Assert.NotEqual(first, second);
        using var old = await ConfirmAsync("de", first);
        Assert.Equal("/de/signup/confirm?status=invalid", old.Headers.Location?.OriginalString);
        using var current = await ConfirmAsync("de", second);
        Assert.Equal("/de/signup/confirm?status=confirmed", current.Headers.Location?.OriginalString);
    }

    /// <summary>Wer nur die E-Mail-Adresse kennt, kann bestätigte Daten nicht überschreiben.</summary>
    [Fact]
    public async Task Changes_to_confirmed_signups_wait_for_confirmation()
    {
        await ConfirmAsync("de", await SignUpAndGetTokenAsync(genres: "Roguelikes"));

        var token = await SignUpAndGetTokenAsync(genres: "Horror");

        var signup = await factory.WithDbAsync(db => db.Signups.OfType<CreatorSignup>().SingleAsync(Ct));
        Assert.Equal("Roguelikes", signup.Genres); // noch unverändert
        var mail = await factory.WithDbAsync(db => db.OutboxEmails.OrderByDescending(m => m.CreatedAt).FirstAsync(Ct));
        Assert.Equal("Bitte bestätige deine geänderten Angaben bei PubPot", mail.Subject);

        await ConfirmAsync("de", token);

        signup = await factory.WithDbAsync(db => db.Signups.OfType<CreatorSignup>().SingleAsync(Ct));
        Assert.Equal("Horror", signup.Genres);
        Assert.NotNull(signup.ConfirmedAt);
    }

    [Fact]
    public async Task Outbox_sends_due_mails_and_marks_them_sent()
    {
        await SignUpAndGetTokenAsync();

        var processed = await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct));

        Assert.Equal(1, processed);
        var sent = Assert.Single(factory.Mail.Sent);
        Assert.Equal("kim@example.com", sent.To);
        var stored = await factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(Ct));
        Assert.NotNull(stored.SentAt);
        Assert.Equal(1, stored.Attempts);

        // Nichts doppelt verschicken.
        Assert.Equal(0, await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct)));
    }

    [Fact]
    public async Task Failed_mail_is_retried_later()
    {
        await SignUpAndGetTokenAsync();
        factory.Mail.Fail = true;

        await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct));

        var stored = await factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(Ct));
        Assert.Null(stored.SentAt);
        Assert.Equal(1, stored.Attempts);
        Assert.True(stored.NextAttemptAt > DateTimeOffset.UtcNow, "Nächster Versuch muss in der Zukunft liegen.");
        Assert.Contains("IOException", stored.LastError, StringComparison.Ordinal);

        // Sofort erneut: noch nicht fällig → kein Versuch.
        factory.Mail.Fail = false;
        Assert.Equal(0, await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct)));
        Assert.Empty(factory.Mail.Sent);
    }

    /// <summary>Host-Header-Angriff: Links in Mails kommen aus Site:BaseUrl, nie aus der Anfrage.</summary>
    [Fact]
    public async Task Mail_links_use_configured_base_url_not_request_host()
    {
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Site:BaseUrl", "https://pubpot.example"));
        using var client = CreateClient(app, "http://boese.example");

        using var response = await PostFormAsync(client, "/de/signup/creator", CreatorForm());

        var mail = await factory.WithDbAsync(db => db.OutboxEmails.SingleAsync(Ct));
        Assert.Contains("https://pubpot.example/de/signup/confirm?token=", mail.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("boese.example", mail.TextBody, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenInLink();
}
