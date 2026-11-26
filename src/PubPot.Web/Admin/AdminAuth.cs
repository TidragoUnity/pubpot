using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace PubPot.Web.Admin;

/// <summary>
/// Login für den Admin-Bereich: verschlüsseltes Cookie, Prüfung bei jeder Anfrage und Abmelden (E-026).
/// </summary>
public static class AdminAuth
{
    public const string Scheme = "Admin";

    public const string Policy = "Admin";

    public const string PathPrefix = "/admin";

    /// <summary>
    /// <c>__Host-</c>: Der Browser nimmt das Cookie nur mit Secure, ohne Domain und mit Path=/ an.
    /// So kann keine Subdomain es setzen oder überschreiben.
    /// </summary>
    public const string CookieName = "__Host-pubpot-admin";

    private const string SessionIdClaim = "sid";
    private const string IssuedAtClaim = "iat";
    private const string CredentialClaim = "cred";

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<AdminLoginThrottle>();
        services.AddSingleton<AdminSessions>();

        services.AddAuthentication(Scheme).AddCookie(Scheme);
        services.AddOptions<CookieAuthenticationOptions>(Scheme).Configure<IOptions<AdminOptions>>((o, admin) =>
        {
            o.Cookie.Name = CookieName;
            o.Cookie.Path = "/";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Strict: Das Cookie wird nie bei Anfragen von fremden Seiten mitgeschickt (Schutz vor CSRF).
            // Folge: Ein Link aus einer Mail führt erst zum Login, danach direkt zur Seite.
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.LoginPath = PathPrefix + "/login";
            o.LogoutPath = PathPrefix + "/logout";
            o.AccessDeniedPath = PathPrefix + "/login";
            o.ExpireTimeSpan = admin.Value.IdleTimeout;
            o.SlidingExpiration = true;
            o.Events.OnValidatePrincipal = async context =>
            {
                var sessions = context.HttpContext.RequestServices.GetRequiredService<AdminSessions>();
                if (!sessions.IsValid(context.Principal))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(Scheme);
                }
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());

        return services;
    }

    /// <summary>
    /// Vor allen Admin-Seiten: 404, solange kein Passwort-Hash gesetzt ist. Außerdem nie zwischenspeichern
    /// (die Seiten zeigen persönliche Daten), nicht indexieren und immer Deutsch (E-024).
    /// </summary>
    public static IApplicationBuilder UseAdminGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!IsAdminPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (!context.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.IsEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var german = CultureInfo.GetCultureInfo("de");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                return Task.CompletedTask;
            });

            await next(context);
        });

    public static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments(PathPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Benutzername und Passwort prüfen. Dauert immer gleich lang, egal welcher Teil falsch ist.</summary>
    public static bool CheckCredentials(AdminOptions options, string? username, string? password)
    {
        // Feste Länge durch den Hash: FixedTimeEquals verrät so auch nichts über die Länge des Benutzernamens.
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(options.Username));
        var given = SHA256.HashData(Encoding.UTF8.GetBytes(username?.Trim() ?? ""));
        var userMatches = CryptographicOperations.FixedTimeEquals(expected, given);

        // Das Passwort wird immer geprüft, auch bei falschem Namen. Sonst wäre die Antwort messbar schneller.
        var passwordMatches = AdminPassword.Verify(options.PasswordHash, password ?? "");
        return userMatches && passwordMatches;
    }

    public static ClaimsPrincipal CreatePrincipal(AdminOptions options, DateTimeOffset now)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, options.Username),
                new Claim(SessionIdClaim, Guid.NewGuid().ToString("N")),
                new Claim(IssuedAtClaim, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                new Claim(CredentialClaim, CredentialFingerprint(options)),
            ],
            Scheme);
        return new ClaimsPrincipal(identity);
    }

    internal static string? SessionId(ClaimsPrincipal? principal) => principal?.FindFirstValue(SessionIdClaim);

    internal static DateTimeOffset? IssuedAt(ClaimsPrincipal? principal) =>
        long.TryParse(principal?.FindFirstValue(IssuedAtClaim), CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    internal static string? Credential(ClaimsPrincipal? principal) => principal?.FindFirstValue(CredentialClaim);

    /// <summary>
    /// Kurzer Fingerabdruck von Benutzername und Passwort-Hash. Ändert sich das Passwort,
    /// passen alte Cookies nicht mehr und alle bestehenden Sitzungen enden.
    /// </summary>
    internal static string CredentialFingerprint(AdminOptions options) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Username + "\n" + options.PasswordHash)), 0, 8);
}

/// <summary>
/// Prüft bei jeder Anfrage, ob eine Sitzung noch gilt: richtiges Konto, nicht zu alt, nicht abgemeldet.
/// </summary>
/// <remarks>
/// Ein Cookie trägt alle Angaben selbst. Ohne diese Liste wäre ein kopiertes Cookie nach dem Abmelden
/// weiter gültig, bis es abläuft. Die Liste liegt nur im Arbeitsspeicher (E-026).
/// </remarks>
public sealed class AdminSessions(IOptions<AdminOptions> options, TimeProvider time)
{
    // Abgemeldete Sitzungen mit dem Zeitpunkt, ab dem sie ohnehin abgelaufen wären.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new();

    public bool IsValid(ClaimsPrincipal? principal)
    {
        var admin = options.Value;
        var now = time.GetUtcNow();
        return admin.IsEnabled
            && AdminAuth.SessionId(principal) is { } sessionId
            && !_revoked.ContainsKey(sessionId)
            && AdminAuth.IssuedAt(principal) is { } issuedAt
            && issuedAt <= now
            && now - issuedAt < admin.MaxSessionLifetime
            && AdminAuth.Credential(principal) == AdminAuth.CredentialFingerprint(admin);
    }

    public void Revoke(ClaimsPrincipal? principal)
    {
        if (AdminAuth.SessionId(principal) is not { } sessionId)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var (id, expires) in _revoked)
        {
            if (expires <= now)
            {
                _revoked.TryRemove(id, out _);
            }
        }

        var issuedAt = AdminAuth.IssuedAt(principal) ?? now;
        _revoked[sessionId] = issuedAt + options.Value.MaxSessionLifetime;
    }
}
