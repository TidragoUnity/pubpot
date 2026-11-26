using System.Net;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using PubPot.Web.Signups;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Entwickler-Formular gegen eine echte Datenbank.</summary>
[Collection("Database")]
public class DeveloperSignupTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);

    // xUnit erzeugt pro Test eine neue Instanz → vor jedem Test leere Tabellen.
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Dictionary<string, string> ValidForm(string email = "dev@example.com") => new()
    {
        ["Input.Name"] = "Testa Entwicklerin",
        ["Input.Email"] = email,
        ["Input.Studio"] = "Pixel Kollektiv",
        ["Input.GameTitle"] = "Sternenfeld",
        ["Input.SteamLink"] = "https://store.steampowered.com/app/1234560/Sternenfeld/",
        ["Input.Release"] = ReleaseChoice.Released,
        ["Input.HasDemo"] = "true",
        ["Input.NextFestPilot"] = "true",
        ["Input.HasPublisher"] = "false",
        ["Input.Message"] = "  Hallo!  ",
        ["Input.Consent"] = "true",
    };

    [Fact]
    public async Task Valid_signup_is_saved_and_redirects_to_thanks()
    {
        using var response = await PostFormAsync(_client, "/de/signup/developer", ValidForm());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/de/signup/thanks", response.Headers.Location?.OriginalString);

        var saved = Assert.Single(await factory.WithDbAsync(db => db.Signups.OfType<DeveloperSignup>().ToListAsync(Ct)));
        Assert.Equal("dev@example.com", saved.NormalizedEmail);
        Assert.Equal("Testa Entwicklerin", saved.Name);
        Assert.Equal("de", saved.Language);
        Assert.Equal("Pixel Kollektiv", saved.Studio);
        Assert.Equal(1234560, saved.SteamAppId);
        Assert.True(saved.AlreadyReleased);
        Assert.Null(saved.PlannedRelease);
        Assert.True(saved.HasDemo);
        Assert.True(saved.InterestedInNextFestPilot);
        Assert.False(saved.HasPublisher);
        Assert.Equal("Hallo!", saved.Message);
        Assert.Equal(ConsentText.Version, saved.ConsentVersion);
        Assert.Null(saved.ConfirmedAt); // erst nach Double-Opt-in bestätigt
    }

    [Fact]
    public async Task English_signup_remembers_language()
    {
        var form = ValidForm();
        form["Input.Release"] = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

        using var response = await PostFormAsync(_client, "/en/signup/developer", form);

        Assert.Equal("/en/signup/thanks", response.Headers.Location?.OriginalString);
        var saved = Assert.Single(await factory.WithDbAsync(db => db.Signups.OfType<DeveloperSignup>().ToListAsync(Ct)));
        Assert.Equal("en", saved.Language);
        Assert.False(saved.AlreadyReleased);
        Assert.NotNull(saved.PlannedRelease);
        Assert.Equal(1, saved.PlannedRelease!.Value.Day);
    }

    [Fact]
    public async Task Same_email_is_merged_instead_of_duplicated()
    {
        await PostFormAsync(_client, "/de/signup/developer", ValidForm("dev@example.com"));
        var first = await factory.WithDbAsync(db => db.Signups.SingleAsync(Ct));

        var second = ValidForm("  DEV@Example.COM ");
        second["Input.GameTitle"] = "Sternenfeld 2";
        await PostFormAsync(_client, "/de/signup/developer", second);

        var saved = Assert.Single(await factory.WithDbAsync(db => db.Signups.OfType<DeveloperSignup>().ToListAsync(Ct)));
        Assert.Equal(first.Id, saved.Id);
        Assert.Equal("Sternenfeld 2", saved.GameTitle);
        Assert.Equal(first.CreatedAt, saved.CreatedAt);
        Assert.True(saved.UpdatedAt >= first.UpdatedAt);
    }

    [Fact]
    public async Task Missing_fields_show_localized_errors_and_save_nothing()
    {
        // So schickt ein Browser ein leeres Formular: Textfelder leer, nicht gewählte Radiobuttons fehlen,
        // die Checkbox sendet über ihr verstecktes Feld "false".
        Dictionary<string, string> emptyForm = new()
        {
            ["Input.Name"] = "",
            ["Input.Email"] = "",
            ["Input.Studio"] = "",
            ["Input.GameTitle"] = "",
            ["Input.SteamLink"] = "",
            ["Input.Release"] = "",
            ["Input.Message"] = "",
            ["Input.Consent"] = "false",
        };

        using var response = await PostFormAsync(_client, "/en/signup/developer", emptyForm);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please fill in this field.", html, StringComparison.Ordinal);
        Assert.Contains("Please choose an option.", html, StringComparison.Ordinal);
        Assert.Contains("We can&#x27;t contact you without your consent.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Validation.", html, StringComparison.Ordinal); // keine unübersetzten Schlüssel
        Assert.Contains("aria-invalid=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    /// <summary>
    /// Ein Bot schickt gar keine Felder: ASP.NET bindet "Input" dann nicht und prüft die Felder nicht einzeln –
    /// es gibt nur einen Fehler am Objekt selbst. Entscheidend: Es wird nichts gespeichert, kein Absturz.
    /// </summary>
    [Fact]
    public async Task Completely_empty_post_saves_nothing()
    {
        using var response = await PostFormAsync(_client, "/de/signup/developer", []);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Theory]
    [InlineData("Input.SteamLink", "https://example.com/app/123", "store.steampowered.com/app/123456")]
    [InlineData("Input.Email", "keine-mail", "gültige E-Mail-Adresse")]
    [InlineData("Input.Email", "a@b", "gültige E-Mail-Adresse")]
    [InlineData("Input.Release", "2020-01", "Eintrag aus der Liste")]
    [InlineData("Input.Release", "irgendwas", "Eintrag aus der Liste")]
    [InlineData("Input.HasDemo", "vielleicht", "Bitte prüfe diese Angabe.")]
    public async Task Invalid_values_are_rejected(string field, string value, string expectedError)
    {
        var form = ValidForm();
        form[field] = value;

        using var response = await PostFormAsync(_client, "/de/signup/developer", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, html, StringComparison.Ordinal);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Too_long_input_is_rejected()
    {
        var form = ValidForm();
        form["Input.Name"] = new string('x', 101);

        using var response = await PostFormAsync(_client, "/de/signup/developer", form);

        Assert.Contains("Bitte höchstens 100 Zeichen.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/de/signup/developer")
        {
            Content = new FormUrlEncodedContent(ValidForm()),
        };

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }
}
