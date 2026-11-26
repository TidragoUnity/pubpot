namespace TwitchWorker;

/// <summary>Zugangsdaten und Adressen der Twitch-Schnittstelle (Abschnitt "Twitch").</summary>
public sealed class TwitchOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Nur zum Testen ändern (z. B. auf einen lokalen Mock-Server).
    public string AuthUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string ApiUrl { get; set; } = "https://api.twitch.tv/helix";
}

/// <summary>Was und wie oft abgefragt wird (Abschnitt "Worker").</summary>
public sealed class WorkerOptions
{
    /// <summary>Abstand zwischen zwei Abfragen in Minuten.</summary>
    public double IntervalMinutes { get; set; } = 5;

    /// <summary>Sprachen der Streams, kommagetrennt, z. B. "de" oder "de,en".</summary>
    public string Languages { get; set; } = "de";

    /// <summary>Optional: nur diese Twitch-Kategorien (Game-IDs), kommagetrennt.</summary>
    public string GameIds { get; set; } = "";

    /// <summary>Optional: nur diese Kanäle (Login-Namen), kommagetrennt. Ersetzt den Sprachfilter.</summary>
    public string UserLogins { get; set; } = "";

    /// <summary>Nach so vielen Stunden ab der ersten Abfrage beenden. 0 = endlos. 168 = eine Woche.</summary>
    public double StopAfterHours { get; set; } = 0;

    /// <summary>Sicherheitsgrenze gegen Endlos-Blättern pro Filter.</summary>
    public int MaxPagesPerFilter { get; set; } = 500;

    /// <summary>Datei, deren Zeitstempel nach jeder Abfrage erneuert wird (für den Docker-Healthcheck).</summary>
    public string HeartbeatFile { get; set; } = Path.Combine(Path.GetTempPath(), "heartbeat");

    internal static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
