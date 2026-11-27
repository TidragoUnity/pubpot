using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;
using PubPot.Web.Monitoring;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Beim Start meldet die App jede eigene Zeitreihe einmal mit 0 (Schritt 7.10, E-059). Ohne die Null beginnt eine Reihe
/// erst beim ersten Ereignis mit dem Wert 1, und <c>increase()</c> in Prometheus übersieht genau dieses Ereignis.
/// </summary>
public sealed class MetricsStartTests
{
    private static async Task<Dictionary<string, List<CollectedMeasurement<long>>>> MeasurementsAfterStartAsync(
        params string[] instruments)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPubPotMetrics();
        using var host = builder.Build();
        var meters = host.Services.GetRequiredService<IMeterFactory>();
        // Die Collector hängen vor dem Start: Geprüft wird der echte Weg über den Host, nicht ein Aufruf von Hand.
        var collectors = instruments.ToDictionary(i => i, i => new MetricCollector<long>(meters, PubPotMetrics.MeterName, i));
        try
        {
            await host.StartAsync(Ct);
            await host.StopAsync(Ct);
            return collectors.ToDictionary(c => c.Key, c => c.Value.GetMeasurementSnapshot().ToList());
        }
        finally
        {
            foreach (var collector in collectors.Values)
            {
                collector.Dispose();
            }
        }
    }

    private static string[] Labels(List<CollectedMeasurement<long>> measurements, string label)
    {
        Assert.All(measurements, m => Assert.Equal(0, m.Value));
        return [.. measurements.Select(m => (string)m.Tags[label]!).Order(StringComparer.Ordinal)];
    }

    [Fact]
    public async Task Every_counter_starts_at_zero_for_every_label_value()
    {
        var measured = await MeasurementsAfterStartAsync(
            "pubpot.signups", "pubpot.signups.confirmed", "pubpot.signups.spam",
            "pubpot.mails.sent", "pubpot.mails.failed", "pubpot.cleanup.deleted");

        Assert.Equal(["creator", "developer"], Labels(measured["pubpot.signups"], "kind"));
        Assert.Equal(["creator", "developer"], Labels(measured["pubpot.signups.confirmed"], "kind"));
        Assert.Equal(["honeypot", "too_fast"], Labels(measured["pubpot.signups.spam"], "reason"));
        Assert.Equal(0, Assert.Single(measured["pubpot.mails.sent"]).Value);
        Assert.Equal(0, Assert.Single(measured["pubpot.mails.failed"]).Value);
        Assert.Equal(CleanupType.All.Order(StringComparer.Ordinal), Labels(measured["pubpot.cleanup.deleted"], "type"));
    }
}
