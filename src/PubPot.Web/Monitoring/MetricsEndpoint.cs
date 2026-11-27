using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace PubPot.Web.Monitoring;

/// <summary>
/// Metriken für Prometheus (Schritt 7.2, E-051): <c>/metrics</c> nur auf einem eigenen Port (Standard 9464).
/// Caddy leitet ausschließlich an Port 8080 weiter, von außen ist der Endpunkt also nicht erreichbar.
/// Prometheus holt die Werte im internen Docker-Netz ab.
/// </summary>
/// <remarks>
/// Entscheidend ist der Port, auf dem die Verbindung wirklich ankam (<c>Connection.LocalPort</c>), nicht der
/// Host-Header. Den kann ein Besucher frei wählen, und Caddy reicht ihn an die App durch.
/// </remarks>
public static class MetricsEndpoint
{
    public const string PortKey = "Metrics:Port";

    public const string Path = "/metrics";

    /// <summary>Messwerte, die neben unseren eigenen Zählern erfasst werden. Alle sind in .NET eingebaut.</summary>
    private static readonly string[] BuiltInMeters =
    [
        "Microsoft.AspNetCore.Hosting", // Anfragen, Antwortzeiten, Statuscodes (nach Route, nie mit IDs)
        "Microsoft.AspNetCore.Server.Kestrel", // offene Verbindungen
        "Microsoft.AspNetCore.RateLimiting", // abgewiesene Anfragen (429)
        "Microsoft.AspNetCore.Diagnostics", // unbehandelte Ausnahmen
        "System.Runtime", // Speicher, Garbage Collector, Threads, CPU
        "System.Net.Http", // ausgehende Aufrufe (Discord)
    ];

    /// <summary>Port für <c>/metrics</c>, 0 = aus.</summary>
    public static int Port(IConfiguration configuration) => configuration.GetValue(PortKey, 9464);

    public static void AddPubPotMetrics(this IServiceCollection services)
    {
        services.AddSingleton<PubPotMetrics>();
        services.AddHostedService<MetricsInitializer>();
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("pubpot-web"))
            .WithMetrics(m => m
                .AddMeter(PubPotMetrics.MeterName)
                .AddMeter(BuiltInMeters)
                // Ohne Zwischenspeicher: Prometheus fragt ohnehin nur alle paar Sekunden.
                .AddPrometheusExporter(o => o.ScrapeResponseCacheDurationMilliseconds = 0));
    }

    /// <summary>
    /// Ganz vorne in der Pipeline: Alles, was auf dem Metrik-Port ankommt, beantwortet dieser Zweig. Dort gibt es
    /// nur <c>/metrics</c>, sonst 404. Die Abfragen selbst zählen nicht als Anfragen der Website.
    /// </summary>
    public static void UseMetricsPort(this WebApplication app, int port)
    {
        if (port <= 0)
        {
            return;
        }

        app.MapWhen(context => context.Connection.LocalPort == port, branch =>
        {
            branch.Use((context, next) =>
            {
                if (context.Features.Get<IHttpMetricsTagsFeature>() is { } tags)
                {
                    tags.MetricsDisabled = true;
                }

                return next(context);
            });
            branch.UseOpenTelemetryPrometheusScrapingEndpoint(context => context.Request.Path == Path);
            branch.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        });
    }
}
