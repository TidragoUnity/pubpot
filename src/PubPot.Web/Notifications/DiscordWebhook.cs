using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace PubPot.Web.Notifications;

/// <summary>
/// Warteschlange für Discord-Hinweise im Arbeitsspeicher (E-030). Höchstens 100 Einträge, bei Überlauf fällt der
/// älteste weg. Nach einem Neustart sind offene Hinweise verloren. Das ist vertretbar, weil die Mail der
/// verlässliche Weg ist (sie liegt in der Datenbank).
/// </summary>
public sealed class DiscordQueue
{
    private readonly Channel<string> _messages = Channel.CreateBounded<string>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Enqueue(string message) => _messages.Writer.TryWrite(message);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken ct) => _messages.Reader.ReadAllAsync(ct);
}

/// <summary>Schickt die Hinweise im Hintergrund an den Discord-Webhook, mit bis zu 3 Versuchen.</summary>
public sealed class DiscordSenderService(
    DiscordQueue queue,
    IHttpClientFactory http,
    IOptions<NotificationOptions> options,
    TimeProvider time,
    ILogger<DiscordSenderService> log) : BackgroundService
{
    public const string HttpClientName = "discord";

    public const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (await TrySendAsync(message, stoppingToken))
                {
                    break;
                }

                if (attempt < MaxAttempts)
                {
                    // 2 s, dann 4 s: genug für kurze Störungen oder ein Rate-Limit von Discord.
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), time, stoppingToken);
                }
                else
                {
                    log.LogWarning("Discord-Hinweis nach {Attempts} Versuchen verworfen.", MaxAttempts);
                }
            }
        }
    }

    private async Task<bool> TrySendAsync(string message, CancellationToken ct)
    {
        try
        {
            using var client = http.CreateClient(HttpClientName);
            // allowed_mentions leer: Auch wenn im Text einmal "@everyone" stünde, pingt Discord niemanden an.
            using var response = await client.PostAsJsonAsync(
                options.Value.DiscordWebhookUrl.Trim(),
                new { content = message, allowed_mentions = new { parse = Array.Empty<string>() } },
                ct);

            if (response.IsSuccessStatusCode)
            {
                log.LogInformation("Discord-Hinweis gesendet.");
                return true;
            }

            // Nur der Statuscode: Die Webhook-URL ist ein Geheimnis und gehört nicht ins Log.
            log.LogWarning("Discord-Hinweis abgelehnt (HTTP {Status}).", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            log.LogWarning("Discord nicht erreichbar ({Error}).", ex.GetType().Name);
            return false;
        }
    }
}
