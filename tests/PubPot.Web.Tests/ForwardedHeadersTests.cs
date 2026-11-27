using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PubPot.Web.Security;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Betrieb hinter Caddy (E-033): Client-IP und HTTPS aus den Forwarded-Headern, aber nur vom vertrauten Proxy.
/// </summary>
public class ForwardedHeadersTests(PubPotWebFactory factory) : IClassFixture<PubPotWebFactory>
{
    private const string ProxyNetwork = "172.30.0.0/24";
    private const string Caddy = "172.30.0.2";
    private const string Stranger = "203.0.113.7";

    /// <summary>
    /// App-Instanz wie in Produktion (HSTS nur außerhalb von Development), mit vertrautem Proxy-Netz.
    /// TestServer kennt keine Absender-IP. Der Header <see cref="FakeRemoteAddress.Header"/> spielt sie vor.
    /// </summary>
    private WebApplicationFactory<Program> App(string? knownNetworks = ProxyNetwork, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production")
                .UseSetting("Site:BaseUrl", "https://pubpot.test")
                .UseSetting(TrustedProxies.ConfigKey, knownNetworks ?? "");
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }

            b.ConfigureTestServices(s => s.AddTransient<IStartupFilter, FakeRemoteAddress>());
        });

    /// <summary>Client, dessen Anfragen von <paramref name="remote"/> kommen, mit den Headern, die Caddy setzen würde.</summary>
    private static HttpClient From(WebApplicationFactory<Program> app, string remote, string? forwardedFor = null, string? forwardedProto = "https")
    {
        var client = CreateClient(app, "http://pubpot.test");
        client.DefaultRequestHeaders.Add(FakeRemoteAddress.Header, remote);
        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        }

        if (forwardedProto is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);
        }

        return client;
    }

    private static async Task<(bool Hsts, bool SecureCookie)> HttpsSignsAsync(HttpClient client)
    {
        // Login-Seite: braucht keine Datenbank und setzt das Antiforgery-Cookie.
        using var response = await GetAsync(client, "/admin/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
        return (response.Headers.Contains("Strict-Transport-Security"),
            cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Https_from_trusted_proxy_enables_hsts_and_secure_cookies()
    {
        using var app = App();
        using var client = From(app, Caddy, forwardedFor: Stranger);

        var (hsts, secure) = await HttpsSignsAsync(client);

        Assert.True(hsts);
        Assert.True(secure);
    }

    /// <summary>Docker meldet IPv4-Adressen bei Dual-Stack oft als <c>::ffff:172.30.0.2</c>.</summary>
    [Fact]
    public async Task Ipv4_mapped_address_of_proxy_is_trusted()
    {
        using var app = App();
        using var client = From(app, "::ffff:" + Caddy, forwardedFor: Stranger);

        var (hsts, secure) = await HttpsSignsAsync(client);

        Assert.True(hsts);
        Assert.True(secure);
    }

    [Fact]
    public async Task Forwarded_headers_from_unknown_sender_are_ignored()
    {
        using var app = App();
        using var client = From(app, Stranger, forwardedFor: "198.51.100.1");

        var (hsts, secure) = await HttpsSignsAsync(client);

        Assert.False(hsts);
        Assert.False(secure);
    }

    /// <summary>Ohne konfigurierte Netze würde ASP.NET JEDEM Absender glauben. Deshalb ist die Middleware dann aus.</summary>
    [Fact]
    public async Task Without_known_networks_forwarded_headers_are_ignored()
    {
        using var app = App(knownNetworks: null);
        using var client = From(app, Caddy, forwardedFor: Stranger);

        var (hsts, secure) = await HttpsSignsAsync(client);

        Assert.False(hsts);
        Assert.False(secure);
    }

    /// <summary>Hinter dem Proxy hat jeder Besucher sein eigenes Kontingent, nicht alle zusammen das von Caddy.</summary>
    [Fact]
    public async Task Rate_limit_counts_per_forwarded_client_ip()
    {
        using var app = App(settings: ("Admin:LoginPostsPerWindow", "2"));
        using var alice = From(app, Caddy, forwardedFor: "198.51.100.1");
        using var bob = From(app, Caddy, forwardedFor: "198.51.100.2");

        Assert.Equal(HttpStatusCode.OK, await FailedLoginAsync(alice));
        Assert.Equal(HttpStatusCode.OK, await FailedLoginAsync(alice));
        Assert.Equal(HttpStatusCode.TooManyRequests, await FailedLoginAsync(alice));
        Assert.Equal(HttpStatusCode.OK, await FailedLoginAsync(bob));
    }

    /// <summary>Wer direkt kommt, kann sich mit einem erfundenen X-Forwarded-For kein neues Kontingent holen.</summary>
    [Fact]
    public async Task Spoofed_forwarded_for_does_not_bypass_rate_limit()
    {
        using var app = App(settings: ("Admin:LoginPostsPerWindow", "2"));

        for (var i = 1; i <= 2; i++)
        {
            using var spoofer = From(app, Stranger, forwardedFor: $"198.51.100.{i}");
            Assert.Equal(HttpStatusCode.OK, await FailedLoginAsync(spoofer));
        }

        using var third = From(app, Stranger, forwardedFor: "198.51.100.3");
        Assert.Equal(HttpStatusCode.TooManyRequests, await FailedLoginAsync(third));
    }

    private static async Task<HttpStatusCode> FailedLoginAsync(HttpClient client)
    {
        using var response = await PostFormAsync(client, "/admin/login", [new("Username", "admin"), new("Password", "falsch")]);
        return response.StatusCode;
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("172.30.0.0/24", 1)]
    [InlineData(" 172.30.0.0/24 , fd00::/64 ", 2)]
    public void Parses_comma_separated_networks(string value, int count) =>
        Assert.Equal(count, TrustedProxies.Parse(value).Count);

    [Theory]
    [InlineData("172.30.0.0")]
    [InlineData("172.30.0.0/99")]
    [InlineData("caddy")]
    public void Invalid_network_stops_the_start(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => TrustedProxies.Parse(value));
        Assert.Contains(TrustedProxies.ConfigKey, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Setzt die Absender-IP aus einem Test-Header, bevor die App-Pipeline läuft.</summary>
    private sealed class FakeRemoteAddress : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var value))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
