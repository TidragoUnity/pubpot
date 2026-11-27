using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using PubPot.Web.Email;
using PubPot.Web.Monitoring;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// <c>/metrics</c> für Prometheus nur auf dem eigenen Port 9464, mit eigenen Zählern und ohne personenbezogene Daten
/// (Schritt 7.2, E-051).
/// </summary>
[Collection("Database")]
public sealed partial class MetricsTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private const int MetricsPort = 9464;

    private const string MetricsPath = "/metrics";

    private readonly HttpClient _client = CreateClient(factory);

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Anfrage, als käme sie über den angegebenen Port herein (siehe <see cref="FakeLocalPort"/>).</summary>
    private async Task<HttpResponseMessage> GetOnPortAsync(string path, int port, string? host = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(FakeLocalPort.Header, port.ToString(CultureInfo.InvariantCulture));
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<string> ScrapeAsync()
    {
        using var response = await GetOnPortAsync(MetricsPath, MetricsPort);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    private static Dictionary<string, string> CreatorForm(string email = "kim@example.com") => new()
    {
        ["Input.Name"] = "Kim Messwert",
        ["Input.Email"] = email,
        ["Input.Platforms"] = "Twitch",
        ["Input.Channel"] = "kim_streamt",
        ["Input.AudienceSize"] = "Under10",
        ["Input.Genres"] = "Roguelikes",
        ["Input.Adult"] = "true",
        ["Input.Consent"] = "true",
    };

    [Fact]
    public async Task Metrics_port_serves_prometheus_format()
    {
        var metrics = await ScrapeAsync();

        Assert.Contains("# TYPE ", metrics, StringComparison.Ordinal);
        // Eingebaute Messwerte von .NET: Speicher und Garbage Collector.
        Assert.Contains("dotnet_gc_", metrics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Website_port_does_not_serve_metrics()
    {
        using var response = await GetOnPortAsync(MetricsPath, 8080);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("# TYPE", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    /// <summary>Über Caddy kann ein Besucher den Host-Header frei wählen. Das darf den Metrik-Port nicht öffnen.</summary>
    [Fact]
    public async Task Faked_host_header_does_not_open_metrics()
    {
        using var response = await GetOnPortAsync(MetricsPath, 8080, host: $"localhost:{MetricsPort}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/de")]
    [InlineData("/health")]
    [InlineData("/admin")]
    public async Task Metrics_port_serves_nothing_else(string path)
    {
        using var response = await GetOnPortAsync(path, MetricsPort);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Die Zähler werden direkt an den Instrumenten dieser Test-App gemessen (<see cref="MetricCollector{T}"/>).
    /// Über <c>/metrics</c> wäre das im Testlauf unzuverlässig: OpenTelemetry fasst gleichnamige Zähler aller
    /// Test-Apps im Prozess zusammen, und wenn eine andere Test-App endet, verschwindet die gemeinsame Reihe.
    /// In Produktion läuft nur eine App pro Prozess; dort prüft der Smoke-Test den Weg bis <c>/metrics</c>.
    /// </summary>
    [Fact]
    public async Task Counts_signups_confirmations_spam_and_mails_without_personal_data()
    {
        var meters = factory.Services.GetRequiredService<IMeterFactory>();
        using var signups = new MetricCollector<long>(meters, PubPotMetrics.MeterName, "pubpot.signups");
        using var confirmed = new MetricCollector<long>(meters, PubPotMetrics.MeterName, "pubpot.signups.confirmed");
        using var spam = new MetricCollector<long>(meters, PubPotMetrics.MeterName, "pubpot.signups.spam");
        using var sent = new MetricCollector<long>(meters, PubPotMetrics.MeterName, "pubpot.mails.sent");

        using (var signup = await PostFormAsync(_client, "/de/signup/creator", CreatorForm()))
        {
            Assert.Equal(HttpStatusCode.Redirect, signup.StatusCode);
        }

        var bot = CreatorForm("bot@example.com");
        bot["Website"] = "https://spam.example";
        using (var dropped = await PostFormAsync(_client, "/de/signup/creator", bot))
        {
            Assert.Equal(HttpStatusCode.Redirect, dropped.StatusCode);
        }

        await factory.WithServiceAsync<OutboxProcessor, int>(p => p.ProcessDueAsync(Ct));
        var token = TokenInLink().Match(factory.Mail.Sent.Single().TextBody).Groups[1].Value;
        using (var confirm = await PostFormAsync(_client, "/de/signup/confirm", [new("Token", token)], tokenSource: "/de/signup/creator"))
        {
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        }

        Assert.Equal("creator", Assert.Single(signups.GetMeasurementSnapshot()).Tags["kind"]);
        Assert.Equal("creator", Assert.Single(confirmed.GetMeasurementSnapshot()).Tags["kind"]);
        Assert.Equal("honeypot", Assert.Single(spam.GetMeasurementSnapshot()).Tags["reason"]);
        Assert.Equal(1, Assert.Single(sent.GetMeasurementSnapshot()).Value);

        // Was Prometheus zu sehen bekommt, enthält nichts aus dem Formular und kein Token.
        var metrics = await ScrapeAsync();
        foreach (var personal in new[] { "kim@example.com", "bot@example.com", "Kim Messwert", "kim_streamt", token })
        {
            Assert.DoesNotContain(personal, metrics, StringComparison.OrdinalIgnoreCase);
        }
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_-]{43})")]
    private static partial Regex TokenInLink();
}

/// <summary>
/// Setzt in Tests den lokalen Port der Verbindung aus einem Test-Header. Der TestServer kennt keine echten Ports,
/// die App unterscheidet Website und Metriken aber genau daran.
/// </summary>
internal sealed class FakeLocalPort : IStartupFilter
{
    public const string Header = "X-Test-Local-Port";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var value))
            {
                context.Connection.LocalPort = int.Parse(value.ToString(), CultureInfo.InvariantCulture);
            }

            return nextMiddleware(context);
        });
        next(app);
    };
}
