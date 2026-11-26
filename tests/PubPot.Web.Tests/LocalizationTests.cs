using System.Net;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Sprachauswahl, Umschalter, hreflang und Sprachvorschlag.</summary>
public class LocalizationTests(PubPotWebFactory factory) : IClassFixture<PubPotWebFactory>
{
    private readonly HttpClient _client = CreateClient(factory);

    [Theory]
    [InlineData("de-DE,de;q=0.9", null, "/de")]
    [InlineData("en-US,en;q=0.9", null, "/en")]
    [InlineData("fr-FR,fr;q=0.9,en;q=0.5", null, "/en")] // erste unterstützte Sprache zählt
    [InlineData("fr-FR", null, "/de")]                    // nichts Passendes → Deutsch
    [InlineData(null, null, "/de")]                       // kein Header → Deutsch
    [InlineData("de", "pubpot_lang=en", "/en")]           // ausdrücklich gewählt schlägt Browser
    [InlineData("en", "pubpot_lang=xx", "/en")]           // ungültiges Cookie wird ignoriert
    public async Task Root_redirects_to_preferred_language(string? acceptLanguage, string? cookie, string expected)
    {
        using var response = await GetAsync(_client, "/", acceptLanguage, cookie);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
        Assert.Contains("Accept-Language", response.Headers.Vary);
    }

    [Fact]
    public async Task Language_switch_keeps_page_and_remembers_choice()
    {
        using var response = await GetAsync(_client, "/lang/en?returnPath=%2Fde%2Fsignup%2Fcreator");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en/signup/creator", response.Headers.Location?.OriginalString);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("pubpot_lang=en;", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Kein "Open Redirect": returnPath darf nie auf eine fremde Seite führen.</summary>
    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example/de")]
    [InlineData("/\\evil.example")]
    [InlineData("/fr/signup")]
    [InlineData("de/signup")]
    [InlineData("")]
    public async Task Language_switch_rejects_foreign_return_paths(string returnPath)
    {
        using var response = await GetAsync(_client, "/lang/en?returnPath=" + Uri.EscapeDataString(returnPath));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/lang/fr")]
    [InlineData("/fr")]
    [InlineData("/DE")]
    [InlineData("/de/does-not-exist")]
    [InlineData("/de/signup/nope")]
    public async Task Unknown_paths_return_localized_404(string path)
    {
        using var response = await GetAsync(_client, path, acceptLanguage: "en");
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_links_its_language_versions()
    {
        var html = await GetHtmlAsync(_client, "/de/signup/creator");

        Assert.Contains("""<link rel="canonical" href="http://localhost/de/signup/creator" />""", html, StringComparison.Ordinal);
        Assert.Contains("""<link rel="alternate" hreflang="de" href="http://localhost/de/signup/creator" />""", html, StringComparison.Ordinal);
        Assert.Contains("""<link rel="alternate" hreflang="en" href="http://localhost/en/signup/creator" />""", html, StringComparison.Ordinal);
        Assert.Contains("""<link rel="alternate" hreflang="x-default" href="http://localhost/" />""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("noindex", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/de/legal/imprint")]
    [InlineData("/en/legal/privacy")]
    [InlineData("/de/signup/thanks")]
    [InlineData("/en/signup/confirm")]
    public async Task Placeholder_and_flow_pages_are_not_indexed(string path)
    {
        var html = await GetHtmlAsync(_client, path);

        Assert.Contains("""<meta name="robots" content="noindex" />""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"canonical\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suggests_browser_language_on_first_visit()
    {
        var html = await GetHtmlAsync(_client, "/de", acceptLanguage: "en-GB,en;q=0.9");

        Assert.Contains("class=\"notice-bar\" lang=\"en\"", html, StringComparison.Ordinal);
        Assert.Contains("This page is also available in English.", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-DE", null)]               // Browser spricht schon die Seitensprache
    [InlineData("en-GB", "pubpot_lang=de")]   // Besucher hat Deutsch ausdrücklich gewählt
    [InlineData(null, null)]                  // keine Angabe
    public async Task No_suggestion_when_not_needed(string? acceptLanguage, string? cookie)
    {
        var html = await GetHtmlAsync(_client, "/de", acceptLanguage, cookie);

        Assert.DoesNotContain("notice-bar", html, StringComparison.Ordinal);
    }
}
