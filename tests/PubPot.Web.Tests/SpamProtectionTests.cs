using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Honeypot, Mindestausfüllzeit, Rate-Limiting und Sperrfrist für Mails.</summary>
[Collection("Database")]
public class SpamProtectionTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Dictionary<string, string> CreatorForm() => new()
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

    /// <summary>Eigene App-Instanz mit scharf geschalteter Einstellung.</summary>
    private HttpClient ClientWith(string key, string value, out WebApplicationFactory<Program> app)
    {
        app = factory.WithWebHostBuilder(b => b.UseSetting(key, value));
        return CreateClient(app);
    }

    private async Task<(int Signups, int Mails)> CountAsync() => (
        await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)),
        await factory.WithDbAsync(db => db.OutboxEmails.CountAsync(Ct)));

    [Fact]
    public async Task Filled_honeypot_is_silently_dropped()
    {
        var form = CreatorForm();
        form["Website"] = "https://spam.example";

        using var response = await PostFormAsync(CreateClient(factory), "/de/signup/creator", form);

        // Der Bot sieht dieselbe Danke-Seite wie ein Mensch …
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/de/signup/thanks", response.Headers.Location?.OriginalString);
        // … aber es wird nichts gespeichert und keine Mail verschickt.
        Assert.Equal((0, 0), await CountAsync());
    }

    [Fact]
    public async Task Too_fast_submission_is_silently_dropped()
    {
        var client = ClientWith("Spam:MinFillSeconds", "60", out var app);
        using (app)
        {
            using var response = await PostFormAsync(client, "/de/signup/creator", CreatorForm());

            Assert.Equal("/de/signup/thanks", response.Headers.Location?.OriginalString);
            Assert.Equal((0, 0), await CountAsync());
        }
    }

    [Theory]
    [InlineData("")]                       // fehlt
    [InlineData("manipuliert")]            // kein gültiger Schutzwert
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAA")] // sieht echt aus, ist es aber nicht
    public async Task Missing_or_forged_timestamp_asks_to_resend(string formStarted)
    {
        var form = CreatorForm();
        form["FormStarted"] = formStarted;

        using var response = await PostFormAsync(CreateClient(factory), "/de/signup/creator", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Das Formular ist abgelaufen. Bitte sende es noch einmal ab", html, StringComparison.Ordinal);
        Assert.Contains("value=\"kim@example.com\"", html, StringComparison.Ordinal); // Eingaben bleiben erhalten
        Assert.Equal((0, 0), await CountAsync());
    }

    [Fact]
    public async Task Too_many_posts_from_one_address_get_429()
    {
        var client = ClientWith("Spam:MaxPostsPerWindow", "2", out var app);
        using (app)
        {
            for (var i = 0; i < 2; i++)
            {
                using var ok = await PostFormAsync(client, "/de/signup/creator", CreatorForm());
                Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
            }

            using var blocked = await PostFormAsync(client, "/de/signup/creator", CreatorForm());
            var html = await blocked.Content.ReadAsStringAsync(Ct);

            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
            Assert.Contains("Bitte warte einen Moment.", html, StringComparison.Ordinal);
            Assert.Contains("noindex", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Viewing_pages_is_not_rate_limited()
    {
        var client = ClientWith("Spam:MaxPostsPerWindow", "1", out var app);
        using (app)
        {
            for (var i = 0; i < 10; i++)
            {
                using var response = await GetAsync(client, "/de/signup/creator");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    /// <summary>Wer eine fremde Adresse kennt, soll sie nicht mit Bestätigungsmails fluten können.</summary>
    [Fact]
    public async Task Repeated_signups_send_at_most_one_mail_per_cooldown()
    {
        var client = ClientWith("Spam:MailCooldown", "00:05:00", out var app);
        using (app)
        {
            for (var i = 0; i < 5; i++)
            {
                using var response = await PostFormAsync(client, "/de/signup/creator", CreatorForm());
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }

            Assert.Equal((1, 1), await CountAsync());
            Assert.Equal(1, await factory.WithDbAsync(db => db.SignupConfirmations.CountAsync(Ct)));
        }
    }

    [Fact]
    public async Task Forms_contain_honeypot_and_signed_timestamp()
    {
        foreach (var path in new[] { "/de/signup/developer", "/en/signup/creator" })
        {
            var html = await GetHtmlAsync(CreateClient(factory), path);

            Assert.Contains("<div class=\"hp\" aria-hidden=\"true\">", html, StringComparison.Ordinal);
            Assert.Contains("name=\"Website\" type=\"text\" tabindex=\"-1\"", html, StringComparison.Ordinal);
            Assert.Matches("name=\"FormStarted\" value=\"CfDJ8[^\"]+\"", html); // Data-Protection-Format
        }
    }
}
