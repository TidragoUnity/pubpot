using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace TwitchWorker;

/// <summary>
/// Die eigentliche Schleife: alle paar Minuten alle passenden Streams abfragen und speichern.
/// </summary>
public sealed class SnapshotWorker : BackgroundService
{
    private readonly TwitchClient _twitch;
    private readonly SampleStore _store;
    private readonly WorkerOptions _options;
    private readonly ILogger<SnapshotWorker> _log;

    public SnapshotWorker(
        TwitchClient twitch,
        SampleStore store,
        IOptions<WorkerOptions> options,
        ILogger<SnapshotWorker> log)
    {
        _twitch = twitch;
        _store = store;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await WaitForDatabaseAsync(ct);

        var filters = BuildFilters();
        var interval = TimeSpan.FromMinutes(_options.IntervalMinutes);
        _log.LogInformation("Start: alle {Interval} Minuten, Filter: {Filters}.",
            _options.IntervalMinutes, string.Join(" | ", filters.Select(Describe)));

        // PeriodicTimer hält den Takt: Abfragen starten alle X Minuten, egal wie lange eine dauert.
        // Dauert eine Runde länger als das Intervall, fällt der nächste Takt einfach aus.
        using var timer = new PeriodicTimer(interval);
        var finishedLogged = false;
        do
        {
            // Nach Ablauf der geplanten Laufzeit läuft der Container weiter, fragt aber nichts mehr ab.
            // So startet Docker ihn nicht ständig neu, und nach einem Neustart geht es nicht von vorn los.
            if (await StopTimeReachedAsync(ct))
            {
                if (!finishedLogged)
                {
                    _log.LogInformation("Geplante Laufzeit von {Hours} Stunden erreicht. Keine weiteren Abfragen; Container kann gestoppt werden.",
                        _options.StopAfterHours);
                    finishedLogged = true;
                }
                await WriteHeartbeatAsync(ct);
                continue;
            }

            await RunSnapshotAsync(filters, ct);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task RunSnapshotAsync(List<List<KeyValuePair<string, string>>> filters, CancellationToken ct)
    {
        var takenAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var streams = new Dictionary<string, TwitchStream>();
        var pages = 0;
        var complete = true;

        foreach (var filter in filters)
        {
            try
            {
                foreach (var s in await _twitch.GetAllStreamsAsync(filter, _options.MaxPagesPerFilter, ct))
                {
                    // Beim Blättern verschiebt sich die Liste, weil sich Zuschauerzahlen ändern.
                    // Doppelte Streams werden hier entfernt.
                    streams[s.Id] = s;
                }
                pages += _twitch.LastPageCount;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                complete = false;
                pages += _twitch.LastPageCount;
                _log.LogError(ex, "Abfrage für {Filter} fehlgeschlagen, speichere unvollständige Runde.", Describe(filter));
            }
        }

        watch.Stop();
        var info = new SnapshotInfo(takenAt, pages, watch.Elapsed, complete,
            string.Join(" | ", filters.Select(Describe)));

        try
        {
            await _store.SaveAsync(info, streams.Values, ct);
            await WriteHeartbeatAsync(ct);

            _log.LogInformation("Runde gespeichert: {Streams} Streams, {Viewers} Zuschauer, {Pages} Seiten, {Seconds:F1}s{Flag}.",
                streams.Count, streams.Values.Sum(s => (long)s.ViewerCount), pages, watch.Elapsed.TotalSeconds,
                complete ? "" : " (unvollständig)");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Kein Heartbeat schreiben: Der Docker-Healthcheck merkt dann, dass etwas nicht stimmt.
            _log.LogError(ex, "Speichern fehlgeschlagen.");
        }
    }

    /// <summary>Baut die Filter für "Get Streams". Pro Anfrage sind höchstens 100 Werte je Filter erlaubt.</summary>
    private List<List<KeyValuePair<string, string>>> BuildFilters()
    {
        var logins = WorkerOptions.Split(_options.UserLogins);
        if (logins.Length > 0)
        {
            return logins.Chunk(100)
                .Select(chunk => chunk.Select(l => new KeyValuePair<string, string>("user_login", l.ToLowerInvariant())).ToList())
                .ToList();
        }

        var languages = WorkerOptions.Split(_options.Languages)
            .Select(l => new KeyValuePair<string, string>("language", l)).ToList();
        var games = WorkerOptions.Split(_options.GameIds);

        if (games.Length == 0)
            return [languages];

        return games.Chunk(100)
            .Select(chunk => languages.Concat(chunk.Select(g => new KeyValuePair<string, string>("game_id", g))).ToList())
            .ToList();
    }

    private static string Describe(List<KeyValuePair<string, string>> filter) =>
        filter.Count == 0
            ? "alle Streams"
            : string.Join(", ", filter.GroupBy(kv => kv.Key).Select(g =>
                g.Count() <= 3 ? $"{g.Key}={string.Join("/", g.Select(kv => kv.Value))}" : $"{g.Key}: {g.Count()} Werte"));

    private async Task WriteHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            await File.WriteAllTextAsync(_options.HeartbeatFile, DateTimeOffset.UtcNow.ToString("O"), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nur für den Healthcheck wichtig, darf den Worker nicht stoppen.
            _log.LogWarning("Heartbeat-Datei {File} nicht schreibbar: {Message}", _options.HeartbeatFile, ex.Message);
        }
    }

    private async Task<bool> StopTimeReachedAsync(CancellationToken ct)
    {
        if (_options.StopAfterHours <= 0)
            return false;

        var first = await _store.GetFirstSnapshotTimeAsync(ct);
        return first is { } start && DateTimeOffset.UtcNow >= start.AddHours(_options.StopAfterHours);
    }

    /// <summary>
    /// Beim Start mit Docker ist die Datenbank oft noch nicht bereit. Dann kurz warten.
    /// Fehlt das Schema nach allen Versuchen noch, bricht der Worker mit klarer Meldung ab.
    /// </summary>
    private async Task WaitForDatabaseAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var missing = await _store.MissingTablesAsync(ct);
                if (missing.Count == 0)
                    return;

                throw new InvalidOperationException(
                    $"Tabellen fehlen: {string.Join(", ", missing)}. Erst das Schema einspielen (Compose-Dienst migrate-twitch, deploy/db/twitch).");
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                _log.LogWarning("Datenbank noch nicht bereit ({Message}), neuer Versuch in 2s.", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
