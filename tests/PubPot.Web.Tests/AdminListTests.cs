using System.Net;
using PubPot.Web.Admin;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Anmeldeliste im Admin-Bereich: Filter, Suche (nur per POST), Seiten, sichere Ausgabe.</summary>
[Collection("Database")]
public class AdminListTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);
    private string _cookie = "";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _cookie = await LoginAsAdminAsync(_client);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task SeedAsync(params Signup[] signups) =>
        await factory.WithDbAsync(async db =>
        {
            db.Signups.AddRange(signups);
            return await db.SaveChangesAsync(Ct);
        });

    private async Task<string> ListAsync(string query = "")
    {
        using var response = await GetAsync(_client, "/admin" + query, cookie: _cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<string> SearchAsync(string search, params KeyValuePair<string, string>[] more)
    {
        using var response = await PostFormAsync(_client, "/admin", [new("Search", search), .. more], cookie: _cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task SeedMixAsync()
    {
        var unconfirmed = TestData.Creator("unbestaetigt@example.com", "Ute Offen", confirmed: false);
        unconfirmed.Channel = "ute_live";
        var contacted = TestData.Developer("pixel@example.com", "Paula Pixel");
        contacted.PipelineStatus = PipelineStatus.Contacted;
        contacted.CreatedAt = TestData.Now.AddDays(-1);
        await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal"), contacted, unconfirmed);
    }

    [Fact]
    public async Task Shows_all_signups_newest_first_with_german_labels()
    {
        await SeedMixAsync();

        var html = await ListAsync();

        Assert.Contains("3 Anmeldungen", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("Kim Kanal", StringComparison.Ordinal) < html.IndexOf("Paula Pixel", StringComparison.Ordinal));
        Assert.Contains("kontaktiert", html, StringComparison.Ordinal);
        Assert.Contains("Pixel Forge / Tiny Dungeon", html, StringComparison.Ordinal);
        Assert.Contains("kim_streamt", html, StringComparison.Ordinal);
        Assert.Contains("nein", html, StringComparison.Ordinal); // unbestätigt
    }

    [Theory]
    [InlineData("?kind=Developer", new[] { "Paula Pixel" })]
    [InlineData("?kind=Creator", new[] { "Kim Kanal", "Ute Offen" })]
    [InlineData("?confirmed=false", new[] { "Ute Offen" })]
    [InlineData("?confirmed=true", new[] { "Kim Kanal", "Paula Pixel" })]
    [InlineData("?status=Contacted", new[] { "Paula Pixel" })]
    [InlineData("?status=New&kind=Creator&confirmed=true", new[] { "Kim Kanal" })]
    [InlineData("?language=en", new[] { "Paula Pixel" })]
    [InlineData("?language=fr", new[] { "Kim Kanal", "Paula Pixel", "Ute Offen" })] // unbekannte Sprache = kein Filter
    public async Task Filters_from_the_url(string query, string[] expected)
    {
        await SeedMixAsync();

        var html = await ListAsync(query);

        foreach (var name in new[] { "Kim Kanal", "Paula Pixel", "Ute Offen" })
        {
            Assert.Equal(expected.Contains(name), html.Contains(name, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("pixel forge", "Paula Pixel")] // Studio, Groß-/Kleinschreibung egal
    [InlineData("tiny", "Paula Pixel")] // Spiel
    [InlineData("KIM_STREAMT", "Kim Kanal")] // Kanal
    [InlineData("unbestaetigt@", "Ute Offen")] // E-Mail
    [InlineData("Ute", "Ute Offen")] // Name
    public async Task Search_finds_name_email_studio_game_and_channel(string search, string expected)
    {
        await SeedMixAsync();

        var html = await SearchAsync(search);

        Assert.Contains("1 Anmeldung (gefiltert)", html, StringComparison.Ordinal);
        Assert.Contains(expected, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_as_normal_characters()
    {
        await SeedMixAsync();

        Assert.Contains("0 Anmeldungen", await SearchAsync("%"), StringComparison.Ordinal);
        Assert.Contains("1 Anmeldung (gefiltert)", await SearchAsync("_streamt"), StringComparison.Ordinal);
        Assert.Contains("Keine Anmeldungen gefunden", await SearchAsync("k_m"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_combines_with_filters()
    {
        await SeedMixAsync();

        var html = await SearchAsync("example.com", new("Kind", "Creator"), new("Confirmed", "false"));

        Assert.Contains("1 Anmeldung (gefiltert)", html, StringComparison.Ordinal);
        Assert.Contains("Ute Offen", html, StringComparison.Ordinal);
    }

    /// <summary>Der Suchtext gehört nicht in die URL – weder als Weiterleitung noch aus einem Link.</summary>
    [Fact]
    public async Task Search_never_travels_in_the_url()
    {
        await SeedMixAsync();

        // Ohne Suchtext: Weiterleitung auf die GET-Adresse mit den Filtern.
        using var filtered = await PostFormAsync(_client, "/admin",
            [new("Search", "  "), new("Kind", "Developer"), new("Language", "en")], cookie: _cookie);
        Assert.Equal(HttpStatusCode.Redirect, filtered.StatusCode);
        Assert.Equal("/admin?kind=Developer&language=en", filtered.Headers.Location?.OriginalString);

        // Ein Suchtext in der URL wird ignoriert.
        var html = await ListAsync("?search=Kim");
        Assert.Contains("3 Anmeldungen", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Kim\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_of_fifty_and_out_of_range_pages_are_clamped()
    {
        var many = Enumerable.Range(1, AdminSignups.PageSize + 1)
            .Select(i =>
            {
                var s = TestData.Creator($"c{i:D3}@example.com", $"Creator {i:D3}");
                s.CreatedAt = TestData.Now.AddMinutes(-i);
                return (Signup)s;
            })
            .ToArray();
        await SeedAsync(many);

        var first = await ListAsync();
        var second = await ListAsync("?p=2");
        var beyond = await ListAsync("?p=99");

        Assert.Contains("Seite 1 von 2", first, StringComparison.Ordinal);
        Assert.Contains("Creator 001", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Creator 051", first, StringComparison.Ordinal);
        Assert.Contains("Creator 051", second, StringComparison.Ordinal);
        Assert.DoesNotContain("Creator 050", second, StringComparison.Ordinal);
        Assert.Contains("Seite 2 von 2", beyond, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_are_shown_as_text_never_as_html()
    {
        await SeedAsync(TestData.Creator("xss@example.com", "<script>alert(1)</script>"));

        using var response = await GetAsync(_client, "/admin", cookie: _cookie);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.DoesNotContain("<script>alert", raw, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_list_says_so()
    {
        Assert.Contains("Keine Anmeldungen gefunden", await ListAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("50%", @"50\%")]
    [InlineData("a_b", @"a\_b")]
    [InlineData(@"c:\x", @"c:\\x")]
    public void Like_patterns_are_escaped(string input, string expected)
    {
        Assert.Equal(expected, AdminSignups.EscapeLike(input));
    }
}
