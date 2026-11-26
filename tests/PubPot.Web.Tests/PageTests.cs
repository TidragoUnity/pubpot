using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Alle Seiten sind erreichbar, vollständig übersetzt und untereinander richtig verlinkt.</summary>
public partial class PageTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = CreateClient(factory);

    [Theory]
    [MemberData(nameof(TestSupport.AllLocalizedPages), MemberType = typeof(TestSupport))]
    public async Task Page_renders_in_its_language(string path)
    {
        using var response = await GetAsync(_client, path);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"<html lang=\"{path[1..3]}\">", html, StringComparison.Ordinal);
        Assert.Matches("<title>[^<]+ – PubPot</title>", html);
        Assert.Contains("<h1", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fehlt ein Text in der .resx, gibt der Localizer den Schlüssel selbst aus (z. B. "How.Step1.Title").
    /// Gerade bei zusammengesetzten Schlüsseln (L[$"How.{step}.Title"]) fällt das nur beim Rendern auf.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestSupport.AllLocalizedPages), MemberType = typeof(TestSupport))]
    public async Task Page_shows_no_raw_resource_keys(string path)
    {
        var html = await GetHtmlAsync(_client, path);

        var leaks = ResourceKeyInText().Matches(html).Select(m => m.Groups[1].Value)
            .Concat(ResourceKeyInAttribute().Matches(html).Select(m => m.Groups[1].Value))
            .ToList();

        Assert.Empty(leaks);
    }

    /// <summary>Ein kleiner Crawler: Jeder interne Link auf jeder Seite muss funktionieren.</summary>
    [Fact]
    public async Task All_internal_links_work()
    {
        var queue = new Queue<string>(["/de", "/en"]);
        var seen = new HashSet<string>(queue);
        var broken = new List<string>();

        while (queue.TryDequeue(out var path))
        {
            using var response = await GetAsync(_client, path);
            var status = (int)response.StatusCode;

            // Seiten: 200. Sprachumschalter und "/": Weiterleitung.
            var expectRedirect = path == "/" || path.StartsWith("/lang/", StringComparison.Ordinal);
            if (expectRedirect ? status != 302 : status != 200)
            {
                broken.Add($"{path} → {status}");
                continue;
            }

            if (expectRedirect)
            {
                continue;
            }

            var html = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("href=\"\"", html, StringComparison.Ordinal);

            foreach (Match match in AnchorHref().Matches(html))
            {
                var href = WebUtility.HtmlDecode(match.Groups[1].Value).Split('#')[0];
                if (href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) && seen.Add(href))
                {
                    queue.Enqueue(href);
                }
            }
        }

        Assert.Empty(broken);
        Assert.True(seen.Count >= 14, $"Nur {seen.Count} Links gefunden – fehlen Seiten in der Navigation?");
    }

    // Text zwischen Tags, der wie ein Schlüssel aussieht: "Nav.How", "Faq.Cost.Q" …
    [GeneratedRegex(@">\s*([A-Z][A-Za-z0-9]*(?:\.[A-Z0-9][A-Za-z0-9]*)+)\s*<")]
    private static partial Regex ResourceKeyInText();

    [GeneratedRegex(@"(?:aria-label|content|title)=""([A-Z][A-Za-z0-9]*(?:\.[A-Z0-9][A-Za-z0-9]*)+)""")]
    private static partial Regex ResourceKeyInAttribute();

    [GeneratedRegex(@"<a\b[^>]*\bhref=""([^""]*)""")]
    private static partial Regex AnchorHref();
}
