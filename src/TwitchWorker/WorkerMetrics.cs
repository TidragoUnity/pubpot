using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace TwitchWorker;

/// <summary>
/// Messwerte des Workers für Prometheus (Schritt 7.3, E-052). Daraus entsteht später der Alarm
/// „Worker seit 15 Minuten ohne vollständige Runde“.
/// </summary>
public sealed class WorkerMetrics
{
    public const string MeterName = "TwitchWorker";

    private readonly Counter<long> _rounds;
    private readonly TimeProvider _time;
    private long _lastRoundStreams = -1;
    // Ausgangswert ist die Startzeit: Der Alarm zählt die 15 Minuten ab dem Start, nicht ab „nie“.
    private double _lastCompleteRound;
    private int _finished;

    public WorkerMetrics(IMeterFactory meterFactory, TimeProvider time)
    {
        _time = time;
        _lastCompleteRound = UnixSeconds(time.GetUtcNow());
        var meter = meterFactory.Create(MeterName);
        _rounds = meter.CreateCounter<long>("pubpot.worker.rounds", "{round}",
            "Abfragerunden nach Ergebnis: complete, incomplete (eine Abfrage fehlgeschlagen) oder failed (Speichern fehlgeschlagen).");
        meter.CreateObservableGauge("pubpot.worker.last_round.streams", ObserveLastRoundStreams, "{stream}",
            "Anzahl Streams in der zuletzt gespeicherten Runde.");
        meter.CreateObservableGauge("pubpot.worker.last_complete_round.time", () => Volatile.Read(ref _lastCompleteRound), "s",
            "Zeitpunkt der letzten vollständigen Runde (Unix-Zeit, vor der ersten Runde: Startzeit).");
        meter.CreateObservableGauge("pubpot.worker.finished", () => Volatile.Read(ref _finished), null,
            "1, wenn die geplante Laufzeit (Worker__StopAfterHours) erreicht ist und der Worker absichtlich nichts mehr abfragt.");
    }

    public void RoundSaved(bool complete, int streams)
    {
        _rounds.Add(1, Result(complete ? "complete" : "incomplete"));
        Volatile.Write(ref _lastRoundStreams, streams);
        if (complete)
        {
            Volatile.Write(ref _lastCompleteRound, UnixSeconds(_time.GetUtcNow()));
        }
    }

    public void RoundFailed() => _rounds.Add(1, Result("failed"));

    public void Finished() => Volatile.Write(ref _finished, 1);

    private IEnumerable<Measurement<long>> ObserveLastRoundStreams()
    {
        var streams = Volatile.Read(ref _lastRoundStreams);
        return streams < 0 ? [] : [new Measurement<long>(streams)];
    }

    private static KeyValuePair<string, object?> Result(string value) => new("result", value);

    private static double UnixSeconds(DateTimeOffset time) => time.ToUnixTimeMilliseconds() / 1000.0;
}

public static class WorkerTelemetry
{
    /// <summary>
    /// Zähler registrieren und, wenn <c>Metrics:Port</c> größer 0 ist (Standard 9464), <c>/metrics</c> per HttpListener
    /// anbieten. Der Port ist nur im internen Docker-Netz erreichbar, der Worker hat keine Ports nach außen.
    /// <c>Metrics:Host</c>: <c>*</c> im Container, in Tests <c>localhost</c> (Windows erlaubt <c>*</c> nur Admins).
    /// </summary>
    public static void AddWorkerMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkerMetrics>();

        var port = configuration.GetValue("Metrics:Port", 9464);
        if (port <= 0)
        {
            return;
        }

        var host = configuration.GetValue("Metrics:Host", "*");
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("pubpot-worker"))
            .WithMetrics(m => m
                .AddMeter(WorkerMetrics.MeterName, "System.Runtime", "System.Net.Http")
                .AddPrometheusHttpListener(o =>
                {
                    o.Port = port;
                    o.ScrapeResponseCacheDurationMilliseconds = 0;
                    if (host == "*")
                    {
                        // Der Exporter baut aus Host und Port eine URI, und "*" ist dafür kein gültiger Hostname.
                        // Deshalb erst mit localhost anlegen und dann durch "alle Adressen" ersetzen.
                        o.Host = "localhost";
                        o.ConfigureHttpListener = (_, listener) =>
                        {
                            listener.Prefixes.Clear();
                            listener.Prefixes.Add($"http://*:{port}/");
                        };
                    }
                    else
                    {
                        o.Host = host;
                    }
                }));
    }
}
