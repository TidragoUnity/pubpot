using System.Net;
using Microsoft.AspNetCore.Hosting;
using PubPot.Web.Security;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Security-Header, keine externen Einbindungen, Health-Endpunkt.</summary>
public class SecurityTests(PubPotWebFactory factory) : IClassFixture<PubPotWebFactory>
{
    private readonly HttpClient _client = CreateClient(factory);

    [Theory]
    [InlineData("/de")]
    [InlineData("/en/legal/privacy")]
    [InlineData("/de/does-not-exist")]
    [InlineData("/health")]
    public async Task Responses_carry_security_headers(string path)
    {
        using var response = await GetAsync(_client, path);

        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Static_files_carry_security_headers()
    {
        var html = await GetHtmlAsync(_client, "/de");
        var css = LinkAttribute().Matches(html).Select(m => m.Groups[1].Value).First(v => v.EndsWith(".css", StringComparison.Ordinal));

        using var response = await GetAsync(_client, css);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Content_security_policy_is_strict()
    {
        // Kein 'unsafe-inline', kein 'unsafe-eval', keine fremden Quellen.
        Assert.DoesNotContain("unsafe", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("http", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("*", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);

        // Die CSP verbietet Inline-Styles und Inline-Skripte – also dürfen auch keine im HTML stehen.
        foreach (var path in Cultures.SelectMany(c => PagePaths.Select(p => $"/{c}{p}")))
        {
            var html = await GetHtmlAsync(_client, path);
            Assert.DoesNotContain(" style=\"", html, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)[^>]*>", html);
            Assert.DoesNotMatch(@"\son[a-z]+=""", html); // onclick= usw.
        }
    }

    /// <summary>Keine Google Fonts, kein CDN, keine Tracker: alles kommt von der eigenen Domain.</summary>
    [Theory]
    [MemberData(nameof(TestSupport.AllLocalizedPages), MemberType = typeof(TestSupport))]
    public async Task Pages_load_nothing_from_other_hosts(string path)
    {
        var html = await GetHtmlAsync(_client, path);

        var external = LinkAttribute().Matches(html)
            .Select(m => m.Groups[1].Value)
            .Where(url => !url.StartsWith('/') && !url.StartsWith('#') && !url.StartsWith("http://localhost/", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(external);
        Assert.DoesNotContain("//", string.Join(' ', LinkAttribute().Matches(html).Select(m => m.Groups[1].Value).Where(u => u.StartsWith("//", StringComparison.Ordinal))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hsts_and_upgrade_are_sent_over_https_in_production()
    {
        // Eigene Instanz im Modus "Production", Aufruf über HTTPS mit echter Domain
        // (für "localhost" setzt ASP.NET bewusst kein HSTS).
        using var production = factory.WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .UseSetting("Site:BaseUrl", "https://pubpot.test")); // außerhalb von Development Pflicht
        using var client = CreateClient(production, "https://pubpot.test");

        using var response = await GetAsync(client, "/health");

        Assert.Equal("max-age=31536000; includeSubDomains", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
        Assert.EndsWith("; upgrade-insecure-requests", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_hsts_over_plain_http()
    {
        using var response = await GetAsync(_client, "/de");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        using var response = await GetAsync(_client, "/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        string Header(string name) =>
            response.Headers.TryGetValues(name, out var values) ? string.Join(",", values)
            : response.Content.Headers.TryGetValues(name, out var contentValues) ? string.Join(",", contentValues)
            : "";

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Header("Content-Security-Policy"));
        Assert.Equal("nosniff", Header("X-Content-Type-Options"));
        Assert.Equal("DENY", Header("X-Frame-Options"));
        Assert.Equal("same-origin", Header("Referrer-Policy"));
        Assert.Equal(SecurityHeaders.PermissionsPolicy, Header("Permissions-Policy"));
        Assert.Equal("same-origin", Header("Cross-Origin-Opener-Policy"));
        Assert.Equal("same-origin", Header("Cross-Origin-Resource-Policy"));
    }
}
