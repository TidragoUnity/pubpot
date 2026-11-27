namespace PubPot.Web;

/// <summary>
/// Docker-Healthcheck ohne <c>curl</c>: <c>dotnet PubPot.Web.dll healthcheck</c> fragt <c>/health</c> der
/// laufenden App im selben Container ab. Exit-Code 0 = gesund, 1 = nicht gesund (E-034).
/// </summary>
/// <remarks>
/// Das Laufzeit-Image ist „chiseled“: ohne Shell, ohne curl und wget. Ein eigener Befehl ist kleiner und sicherer,
/// als dafür Werkzeuge ins Image zu packen.
/// </remarks>
public static class HealthCheckCommand
{
    public const string Name = "healthcheck";

    public const string Path = "/health";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>Port wie Kestrel ihn im Container nutzt (<c>ASPNETCORE_HTTP_PORTS</c>, Standard 8080).</summary>
    public static Uri LocalUri(string? httpPorts)
    {
        var first = (httpPorts ?? "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var port = int.TryParse(first, out var p) && p is > 0 and <= 65535 ? p : 8080;
        return new Uri($"http://127.0.0.1:{port}{Path}");
    }

    public static async Task<int> RunAsync()
    {
        using var client = new HttpClient { Timeout = Timeout };
        return await RunAsync(client, LocalUri(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")), Console.Error);
    }

    public static async Task<int> RunAsync(HttpClient client, Uri uri, TextWriter error)
    {
        try
        {
            using var response = await client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            error.WriteLine($"{uri.AbsolutePath} antwortet mit {(int)response.StatusCode}.");
            return 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            error.WriteLine($"{uri.AbsolutePath} nicht erreichbar: {ex.Message}");
            return 1;
        }
    }
}
