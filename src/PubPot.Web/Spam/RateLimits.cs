using System.Threading.RateLimiting;
using PubPot.Web.Admin;

namespace PubPot.Web.Spam;

/// <summary>
/// Rate-Limiting für Formulare: höchstens N Absendungen (POST) pro IP-Adresse und Zeitfenster.
/// Das Anzeigen der Seiten (GET) ist nicht begrenzt. Zu viele Absendungen → 429 mit freundlicher Seite.
/// </summary>
/// <remarks>
/// WICHTIG für Phase 5: Hinter Caddy sieht die App sonst nur die IP von Caddy – alle Besucher teilten sich
/// dann ein Kontingent. Dort muss die Forwarded-Headers-Middleware die echte Client-IP übernehmen.
/// Die IP wird nur im Arbeitsspeicher als Schlüssel verwendet, nie gespeichert oder geloggt.
/// </remarks>
public static class RateLimits
{
    public const string FormPosts = "form-posts";

    /// <summary>Login-Versuche im Admin-Bereich, zusätzlich zur Sperre des Kontos (E-026).</summary>
    public const string AdminLogin = "admin-login";

    public static IServiceCollection AddFormRateLimiting(this IServiceCollection services, SpamOptions spam, AdminOptions admin) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(FormPosts, context => PostsPerIp(context, spam.MaxPostsPerWindow, spam.Window));
            o.AddPolicy(AdminLogin, context => PostsPerIp(context, admin.LoginPostsPerWindow, admin.LoginWindow));
        });

    private static RateLimitPartition<string> PostsPerIp(HttpContext context, int limit, TimeSpan window) =>
        HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = window,
                    QueueLimit = 0,
                })
            : RateLimitPartition.GetNoLimiter("get");
}
