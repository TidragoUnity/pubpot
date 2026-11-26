namespace PubPot.Web.Security;

/// <summary>
/// Setzt bei jeder Antwort die Sicherheits-Header. Was sie bewirken:
/// <list type="bullet">
/// <item><b>Content-Security-Policy</b>: Der Browser lädt Skripte, Styles, Schriften und Bilder nur von dieser Domain.
/// Kein Inline-JavaScript, keine Inline-Styles. Eingeschleuster Code (XSS) läuft dadurch nicht.</item>
/// <item><b>X-Content-Type-Options</b>: Der Browser rät keine Dateitypen (z. B. Text als Skript ausführen).</item>
/// <item><b>Referrer-Policy</b>: Fremde Seiten erfahren nicht, von welcher Seite jemand kam.</item>
/// <item><b>Permissions-Policy</b>: Kamera, Mikrofon, Standort usw. sind abgeschaltet.</item>
/// <item><b>Cross-Origin-*-Policy</b>: Fremde Seiten können weder unser Fenster noch unsere Dateien einbetten oder steuern.</item>
/// <item><b>X-Frame-Options</b>: Seite darf nicht in einem iframe stecken (Schutz vor Clickjacking; ältere Browser).</item>
/// </list>
/// HSTS setzt <c>app.UseHsts()</c> in Program.cs (nur bei HTTPS).
/// </summary>
public static class SecurityHeaders
{
    public static readonly string ContentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self'",
        "img-src 'self'",
        "font-src 'self'",
        "connect-src 'self'",
        "form-action 'self'",
        "frame-ancestors 'none'",
        "base-uri 'self'",
        "object-src 'none'");

    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Über HTTPS zusätzlich: eingebettete http://-Links automatisch auf https:// heben.
                headers.ContentSecurityPolicy = context.Request.IsHttps
                    ? ContentSecurityPolicy + "; upgrade-insecure-requests"
                    : ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "same-origin";
                headers["Permissions-Policy"] = PermissionsPolicy;
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";
                return Task.CompletedTask;
            });

            await next(context);
        });
}
