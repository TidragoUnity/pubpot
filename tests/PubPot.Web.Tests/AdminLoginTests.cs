using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PubPot.Web.Admin;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Admin-Login: Schutz aller Admin-Seiten, Cookie, Sperre, Abmelden, Rate-Limit (E-026).</summary>
[Collection("Database")]
public class AdminLoginTests(DatabaseWebFactory factory)
{
    /// <summary>Alle Admin-Seiten, die einen Login verlangen. Neue Seiten hier ergänzen.</summary>
    public static readonly TheoryData<string> ProtectedPages = new(["/admin", "/admin/index", "/admin/logout"]);

    private readonly TestClock _clock = new();

    /// <summary>
    /// Eigene App pro Test (gleiche Datenbank): Sperre und abgemeldete Sitzungen liegen im Arbeitsspeicher der App.
    /// </summary>
    private WebApplicationFactory<Program> App(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }

            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock));
        });

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/login")]
    [InlineData("/ADMIN/LOGIN")]
    public async Task Admin_area_does_not_exist_without_password_hash(string path)
    {
        await using var app = App(("Admin:PasswordHash", ""));

        using var response = await GetAsync(CreateClient(app), path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedPages))]
    public async Task Protected_pages_redirect_to_login(string path)
    {
        await using var app = App();

        using var response = await GetAsync(CreateClient(app), path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("/admin/login", location.AbsolutePath);
        Assert.Equal("?ReturnUrl=" + Uri.EscapeDataString(path), location.Query);
    }

    /// <summary>
    /// Sicherheitsnetz: Findet alle Seiten unter Pages/Admin selbst. Jede außer dem Login muss die Admin-Richtlinie haben.
    /// Eine neue Admin-Seite kann so nicht versehentlich ohne Login erreichbar sein.
    /// </summary>
    [Fact]
    public async Task Every_admin_page_except_login_requires_the_admin_policy()
    {
        await using var app = App();
        _ = app.Server; // App starten, damit die Endpunkte feststehen

        var adminPages = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Page: e.Metadata.GetMetadata<PageActionDescriptor>()))
            .Where(x => x.Page?.ViewEnginePath.StartsWith("/Admin/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(adminPages);
        foreach (var (endpoint, page) in adminPages)
        {
            Assert.StartsWith("admin", endpoint.RoutePattern.RawText, StringComparison.OrdinalIgnoreCase);
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy);
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (page!.ViewEnginePath == "/Admin/Login")
            {
                Assert.True(anonymous);
            }
            else
            {
                Assert.Contains(AdminAuth.Policy, policies);
                Assert.False(anonymous, $"{page.ViewEnginePath} ist ohne Login erreichbar.");
            }
        }
    }

    [Fact]
    public async Task Admin_pages_are_not_localized_and_have_no_language_prefix()
    {
        await using var app = App();

        using var response = await GetAsync(CreateClient(app), "/de/admin/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_page_is_german_not_indexed_not_cached_and_csp_clean()
    {
        await using var app = App();

        using var response = await GetAsync(CreateClient(app), "/admin/login", acceptLanguage: "en");
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("Benutzername", html, StringComparison.Ordinal);
        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        AssertCspClean(html);
    }

    [Fact]
    public async Task Correct_login_sets_a_strict_host_cookie_and_opens_the_admin_area()
    {
        await using var app = App();
        var client = CreateClient(app);

        using var login = await PostFormAsync(client, "/admin/login",
            [new("Username", TestAdmin.Username), new("Password", TestAdmin.Password)]);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Headers.Location?.OriginalString);
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-pubpot-admin=", StringComparison.Ordinal));
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", setCookie, StringComparison.OrdinalIgnoreCase); // Sitzungscookie

        using var page = await GetAsync(client, "/admin", cookie: AdminCookie(login));
        var html = await page.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Abmelden", html, StringComparison.Ordinal);
        AssertCspClean(html);
    }

    [Theory]
    [InlineData("admin", "falsches-passwort-123")]
    [InlineData("nicht-admin", TestAdmin.Password)]
    [InlineData("", "")]
    public async Task Wrong_login_shows_an_error_and_sets_no_cookie(string username, string password)
    {
        await using var app = App();

        using var response = await PostFormAsync(CreateClient(app), "/admin/login",
            [new("Username", username), new("Password", password)]);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Benutzername oder Passwort ist falsch.", html, StringComparison.Ordinal);
        Assert.Null(AdminCookie(response));
        if (password.Length > 0)
        {
            Assert.DoesNotContain($"value=\"{password}\"", html, StringComparison.Ordinal); // Passwort nicht zurück ins Formular
        }
    }

    [Fact]
    public async Task Too_many_failures_lock_the_login_even_for_the_right_password_until_the_lockout_ends()
    {
        await using var app = App(("Admin:MaxFailedLogins", "3"), ("Admin:LockoutDuration", "00:15:00"));
        var client = CreateClient(app);

        for (var i = 0; i < 3; i++)
        {
            using var failed = await PostFormAsync(client, "/admin/login", [new("Username", "admin"), new("Password", "falsch-" + i)]);
        }

        using var locked = await PostFormAsync(client, "/admin/login",
            [new("Username", TestAdmin.Username), new("Password", TestAdmin.Password)]);
        var html = WebUtility.HtmlDecode(await locked.Content.ReadAsStringAsync(Ct));
        Assert.Contains("für 15 Minuten gesperrt", html, StringComparison.Ordinal);
        Assert.Null(AdminCookie(locked));

        _clock.Advance(TimeSpan.FromMinutes(15));
        await LoginAsAdminAsync(client);
    }

    /// <summary>Gefunden bei der Abnahme: Es hieß „für 1 Minuten gesperrt“.</summary>
    [Fact]
    public async Task Short_lockout_uses_singular_minute()
    {
        await using var app = App(("Admin:MaxFailedLogins", "1"), ("Admin:LockoutDuration", "00:00:30"));

        using var locked = await PostFormAsync(CreateClient(app), "/admin/login", [new("Username", "admin"), new("Password", "falsch")]);

        Assert.Contains("für 1 Minute gesperrt", WebUtility.HtmlDecode(await locked.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        await using var app = App(("Admin:MaxFailedLogins", "3"));
        var client = CreateClient(app);

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < 2; i++)
            {
                using var failed = await PostFormAsync(client, "/admin/login", [new("Username", "admin"), new("Password", "falsch")]);
            }

            await LoginAsAdminAsync(client); // 2 Fehler, dann Erfolg – zweimal hintereinander, nie gesperrt
        }
    }

    [Fact]
    public async Task Logout_ends_the_session_and_a_copied_cookie_stops_working()
    {
        await using var app = App();
        var client = CreateClient(app);
        var cookie = await LoginAsAdminAsync(client);

        using var logout = await PostFormAsync(client, "/admin/logout", [], tokenSource: "/admin", cookie: cookie);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/admin/login", logout.Headers.Location?.OriginalString);
        using var afterwards = await GetAsync(client, "/admin", cookie: cookie);
        Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
    }

    [Fact]
    public async Task Logout_needs_post_with_antiforgery_token()
    {
        await using var app = App();
        var client = CreateClient(app);
        var cookie = await LoginAsAdminAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/logout");
        request.Headers.Add("Cookie", cookie);
        using var withoutToken = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        using var stillIn = await GetAsync(client, "/admin", cookie: cookie);
        Assert.Equal(HttpStatusCode.OK, stillIn.StatusCode);
    }

    [Fact]
    public async Task Session_ends_after_idle_timeout_and_after_max_lifetime()
    {
        await using var app = App(("Admin:IdleTimeout", "01:00:00"), ("Admin:MaxSessionLifetime", "02:00:00"));
        var client = CreateClient(app);
        var cookie = await LoginAsAdminAsync(client);

        _clock.Advance(TimeSpan.FromMinutes(61));
        using var idle = await GetAsync(client, "/admin", cookie: cookie);
        Assert.Equal(HttpStatusCode.Redirect, idle.StatusCode);

        // Aktiv bleiben (alle 40 Minuten), trotzdem ist nach 2 Stunden Schluss.
        cookie = await LoginAsAdminAsync(client);
        for (var i = 0; i < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(40));
            using var active = await GetAsync(client, "/admin", cookie: cookie);
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            cookie = AdminCookie(active) ?? cookie; // gleitende Verlängerung schickt ein neues Cookie
        }

        _clock.Advance(TimeSpan.FromMinutes(41));
        using var tooOld = await GetAsync(client, "/admin", cookie: cookie);
        Assert.Equal(HttpStatusCode.Redirect, tooOld.StatusCode);
    }

    [Fact]
    public void Changing_the_password_or_username_ends_existing_sessions()
    {
        var old = new AdminOptions { PasswordHash = TestAdmin.PasswordHash };
        var principal = AdminAuth.CreatePrincipal(old, _clock.GetUtcNow());

        Assert.True(new AdminSessions(Options.Create(old), _clock).IsValid(principal));
        var newPassword = new AdminOptions { PasswordHash = AdminPassword.Hash(TestAdmin.Password) };
        Assert.False(new AdminSessions(Options.Create(newPassword), _clock).IsValid(principal));
        var newName = new AdminOptions { PasswordHash = TestAdmin.PasswordHash, Username = "chef" };
        Assert.False(new AdminSessions(Options.Create(newName), _clock).IsValid(principal));
    }

    [Theory]
    [InlineData("/admin/index", "/admin/index")]
    [InlineData("https://boese.example/admin", "/admin")]
    [InlineData("//boese.example/admin", "/admin")]
    [InlineData("/de", "/admin")]
    public async Task Return_url_only_leads_into_the_admin_area(string returnUrl, string expected)
    {
        await using var app = App();

        using var login = await PostFormAsync(CreateClient(app), "/admin/login",
            [new("Username", TestAdmin.Username), new("Password", TestAdmin.Password), new("ReturnUrl", returnUrl)]);

        Assert.Equal(expected, login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_posts_are_rate_limited_per_ip()
    {
        await using var app = App(("Admin:LoginPostsPerWindow", "2"), ("Admin:MaxFailedLogins", "100"));
        var client = CreateClient(app);

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await PostFormAsync(client, "/admin/login", [new("Username", "admin"), new("Password", "falsch")]);
            codes.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], codes);
    }

    private static void AssertCspClean(string html)
    {
        Assert.DoesNotContain(" style=\"", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)[^>]*>", html);
        Assert.DoesNotMatch(@"\son[a-z]+=""", html);
    }
}
