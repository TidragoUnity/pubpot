using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TwitchWorker;

/// <summary>Ein laufender Stream, wie ihn "Get Streams" liefert (nur die Felder, die wir brauchen).</summary>
public sealed record TwitchStream(
    string Id,
    string UserId,
    string UserLogin,
    string GameId,
    string GameName,
    string Language,
    int ViewerCount,
    DateTimeOffset StartedAt);

/// <summary>
/// Spricht die Twitch-Helix-Schnittstelle an: holt einen App-Zugangsschlüssel,
/// blättert durch "Get Streams" und hält sich an das Rate-Limit.
/// </summary>
public sealed class TwitchClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly TwitchOptions _options;
    private readonly ILogger<TwitchClient> _log;

    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public TwitchClient(HttpClient http, IOptions<TwitchOptions> options, ILogger<TwitchClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    /// <summary>Anzahl der Seiten, die beim letzten Aufruf von GetAllStreamsAsync geladen wurden.</summary>
    public int LastPageCount { get; private set; }

    /// <summary>
    /// Holt alle laufenden Streams für einen Filter (z. B. language=de) und blättert bis zum Ende.
    /// Twitch liefert höchstens 100 Streams pro Seite, sortiert nach Zuschauern.
    /// </summary>
    public async Task<List<TwitchStream>> GetAllStreamsAsync(
        IReadOnlyList<KeyValuePair<string, string>> filter, int maxPages, CancellationToken ct)
    {
        var result = new List<TwitchStream>();
        string? cursor = null;
        LastPageCount = 0;

        do
        {
            var query = new List<KeyValuePair<string, string>>(filter) { new("first", "100") };
            if (cursor is not null)
                query.Add(new("after", cursor));

            var page = await GetAsync<StreamsPage>("/streams", query, ct);
            LastPageCount++;

            foreach (var s in page.Data)
            {
                result.Add(new TwitchStream(
                    s.Id, s.UserId, s.UserLogin, s.GameId ?? "", s.GameName ?? "",
                    s.Language ?? "", s.ViewerCount, s.StartedAt));
            }

            cursor = page.Pagination?.Cursor;
            if (page.Data.Count == 0)
                break;
        }
        while (!string.IsNullOrEmpty(cursor) && LastPageCount < maxPages);

        if (LastPageCount >= maxPages)
            _log.LogWarning("Seitenlimit von {MaxPages} erreicht, eventuell fehlen Streams.", maxPages);

        return result;
    }

    private async Task<T> GetAsync<T>(string path, List<KeyValuePair<string, string>> query, CancellationToken ct)
    {
        var url = _options.ApiUrl.TrimEnd('/') + path + "?" + string.Join("&",
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            await EnsureTokenAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Client-Id", _options.ClientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= maxAttempts) throw;
                var wait = Backoff(attempt);
                _log.LogWarning("Netzwerkfehler ({Message}), neuer Versuch in {Wait}s.", ex.Message, wait.TotalSeconds);
                await Task.Delay(wait, ct);
                continue;
            }

            using (response)
            {
                await RespectRateLimitAsync(response, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt < maxAttempts)
                {
                    _log.LogInformation("Zugangsschlüssel abgelaufen, hole einen neuen.");
                    _token = null;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
                {
                    var wait = UntilReset(response) ?? Backoff(attempt);
                    _log.LogWarning("Rate-Limit erreicht, warte {Wait}s.", Math.Ceiling(wait.TotalSeconds));
                    await Task.Delay(wait, ct);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
                {
                    var wait = Backoff(attempt);
                    _log.LogWarning("Twitch antwortet mit {Status}, neuer Versuch in {Wait}s.", (int)response.StatusCode, wait.TotalSeconds);
                    await Task.Delay(wait, ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"Twitch-Fehler {(int)response.StatusCode}: {body}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct)
                       ?? throw new InvalidOperationException("Leere Antwort von Twitch.");
            }
        }
    }

    /// <summary>Holt per "Client Credentials" einen App-Zugangsschlüssel. Reicht für öffentliche Stream-Daten.</summary>
    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "client_credentials",
        });

        using var response = await _http.PostAsync(_options.AuthUrl, form, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Kein Zugangsschlüssel von Twitch ({(int)response.StatusCode}). Client-ID und Secret prüfen. {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, ct)
                    ?? throw new InvalidOperationException("Leere Token-Antwort.");

        _token = token.AccessToken;
        // Eine Stunde Puffer, damit der Schlüssel nicht mitten in einer Abfrage abläuft.
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 3600));
        _log.LogInformation("Neuer Zugangsschlüssel, gültig bis {Expires:u}.", _tokenExpiresAt);
    }

    /// <summary>Wenn fast keine Anfragen mehr übrig sind, bis zum Zurücksetzen warten.</summary>
    private async Task RespectRateLimitAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!TryHeaderInt(response, "Ratelimit-Remaining", out var remaining) || remaining > 2)
            return;

        var wait = UntilReset(response);
        if (wait is { } w && w > TimeSpan.Zero)
        {
            _log.LogInformation("Nur noch {Remaining} Anfragen frei, warte {Wait}s.", remaining, Math.Ceiling(w.TotalSeconds));
            await Task.Delay(w, ct);
        }
    }

    private static TimeSpan? UntilReset(HttpResponseMessage response)
    {
        if (!TryHeaderLong(response, "Ratelimit-Reset", out var resetUnix))
            return null;

        var wait = DateTimeOffset.FromUnixTimeSeconds(resetUnix) - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : wait;
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    private static bool TryHeaderInt(HttpResponseMessage r, string name, out int value)
    {
        value = 0;
        return r.Headers.TryGetValues(name, out var v)
               && int.TryParse(v.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryHeaderLong(HttpResponseMessage r, string name, out long value)
    {
        value = 0;
        return r.Headers.TryGetValues(name, out var v)
               && long.TryParse(v.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // --- JSON-Modelle der Twitch-Antworten ---

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = "";
        public int ExpiresIn { get; set; }
    }

    private sealed class StreamsPage
    {
        public List<StreamDto> Data { get; set; } = [];
        public PaginationDto? Pagination { get; set; }
    }

    private sealed class StreamDto
    {
        public string Id { get; set; } = "";
        public string UserId { get; set; } = "";
        public string UserLogin { get; set; } = "";
        public string? GameId { get; set; }
        public string? GameName { get; set; }
        public string? Language { get; set; }
        public int ViewerCount { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }

    private sealed class PaginationDto
    {
        public string? Cursor { get; set; }
    }
}
