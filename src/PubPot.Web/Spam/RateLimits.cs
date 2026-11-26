using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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

    public static IServiceCollection AddFormRateLimiting(this IServiceCollection services, SpamOptions spam) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(FormPosts, context =>
                HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = spam.MaxPostsPerWindow,
                            Window = spam.Window,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("get"));
        });
}
