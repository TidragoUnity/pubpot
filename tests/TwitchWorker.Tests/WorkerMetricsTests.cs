using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;

namespace TwitchWorker.Tests;

/// <summary>Messwerte des Workers und <c>/metrics</c> per HttpListener (Schritt 7.3, E-052).</summary>
public sealed class WorkerMetricsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class TestTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Start = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static IMeterFactory MeterFactory() =>
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    [Fact]
    public void Counts_rounds_by_result()
    {
        var meters = MeterFactory();
        using var rounds = new MetricCollector<long>(meters, WorkerMetrics.MeterName, "pubpot.worker.rounds");
        var metrics = new WorkerMetrics(meters, new TestTime(Start));

        metrics.RoundSaved(complete: true, streams: 120);
        metrics.RoundSaved(complete: false, streams: 80);
        metrics.RoundFailed();

        Assert.Equal(["complete", "incomplete", "failed"],
            rounds.GetMeasurementSnapshot().Select(m => (string?)m.Tags["result"]));
    }

    /// <summary>Grundlage für den Alarm: Nur vollständige Runden verschieben den Zeitpunkt. Ausgangswert ist der Start.</summary>
    [Fact]
    public void Last_complete_round_starts_at_startup_and_moves_only_on_complete_rounds()
    {
        var meters = MeterFactory();
        using var lastComplete = new MetricCollector<double>(meters, WorkerMetrics.MeterName, "pubpot.worker.last_complete_round.time");
        using var streams = new MetricCollector<long>(meters, WorkerMetrics.MeterName, "pubpot.worker.last_round.streams");
        var time = new TestTime(Start);
        var metrics = new WorkerMetrics(meters, time);

        lastComplete.RecordObservableInstruments();
        streams.RecordObservableInstruments();
        Assert.Equal(Start.ToUnixTimeSeconds(), lastComplete.LastMeasurement!.Value);
        Assert.Null(streams.LastMeasurement); // vor der ersten Runde: keine Zahl statt einer falschen 0

        time.Now = Start.AddMinutes(5);
        metrics.RoundSaved(complete: false, streams: 80);
        lastComplete.RecordObservableInstruments();
        streams.RecordObservableInstruments();
        Assert.Equal(Start.ToUnixTimeSeconds(), lastComplete.LastMeasurement!.Value);
        Assert.Equal(80, streams.LastMeasurement!.Value);

        time.Now = Start.AddMinutes(10);
        metrics.RoundSaved(complete: true, streams: 120);
        lastComplete.RecordObservableInstruments();
        Assert.Equal(Start.AddMinutes(10).ToUnixTimeSeconds(), lastComplete.LastMeasurement!.Value);
    }

    [Fact]
    public void Finished_is_reported_once_the_planned_runtime_is_over()
    {
        var meters = MeterFactory();
        using var finished = new MetricCollector<int>(meters, WorkerMetrics.MeterName, "pubpot.worker.finished");
        var metrics = new WorkerMetrics(meters, new TestTime(Start));

        finished.RecordObservableInstruments();
        Assert.Equal(0, finished.LastMeasurement!.Value);

        metrics.Finished();
        finished.RecordObservableInstruments();
        Assert.Equal(1, finished.LastMeasurement!.Value);
    }

    [Fact]
    public async Task Metrics_endpoint_serves_worker_metrics_in_prometheus_format()
    {
        var port = FreePort();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Metrics:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Metrics:Host"] = "localhost",
        });
        builder.Services.AddWorkerMetrics(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync(Ct);

        host.Services.GetRequiredService<WorkerMetrics>().RoundSaved(complete: true, streams: 42);
        using var http = new HttpClient();
        var metrics = await http.GetStringAsync(new Uri($"http://localhost:{port}/metrics"), Ct);
        await host.StopAsync(Ct);

        Assert.Matches(@"(?m)^pubpot_worker_rounds_total\{[^}]*result=""complete""[^}]*\} 1$", metrics);
        Assert.Matches(@"(?m)^pubpot_worker_last_round_streams\{[^}]*\} 42$", metrics);
        Assert.Matches(@"(?m)^pubpot_worker_last_complete_round_time_seconds\{[^}]*\} \d", metrics);
        Assert.Matches(@"(?m)^pubpot_worker_finished\{[^}]*\} 0$", metrics);
        Assert.Contains("dotnet_gc_", metrics, StringComparison.Ordinal);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
