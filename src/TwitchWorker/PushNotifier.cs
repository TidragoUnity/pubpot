using Microsoft.Extensions.Options;

namespace TwitchWorker;

/// <summary>
/// Ruft nach jeder <b>vollständigen</b> Runde optional eine Push-URL auf (<c>Worker__PushUrl</c>, Schritt 7.3, E-052).
/// Ein externer Dienst (z. B. ein Heartbeat-Monitor) schlägt Alarm, wenn der Aufruf ausbleibt. Das funktioniert auch,
/// wenn der ganze Server ausfällt. Unvollständige Runden melden nichts (Punkt aus E-036).
/// </summary>
/// <remarks>Die URL ist ein Geheimnis (wer sie kennt, kann „alles gut“ melden) und steht nie im Log.</remarks>
public sealed class PushNotifier(HttpClient http, IOptions<WorkerOptions> options, ILogger<PushNotifier> log)
{
    public async Task PushAsync(CancellationToken ct)
    {
        var url = options.Value.PushUrl.Trim();
        if (url.Length == 0)
        {
            return;
        }

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Push-URL antwortet mit HTTP {Status}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            log.LogWarning("Push-URL nicht erreichbar ({Error}).", ex.GetType().Name);
        }
    }

    /// <summary>Leer (aus) oder eine absolute http(s)-Adresse.</summary>
    public static bool IsValid(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http");
}
