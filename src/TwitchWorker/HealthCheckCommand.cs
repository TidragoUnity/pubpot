namespace TwitchWorker;

/// <summary>
/// Docker-Healthcheck ohne Shell: <c>dotnet TwitchWorker.dll healthcheck</c>. Gesund (Exit-Code 0), solange der
/// Worker in den letzten 15 Minuten eine Runde gespeichert hat, also die Heartbeat-Datei frisch ist (E-036).
/// </summary>
/// <remarks>Vorher prüfte das ein <c>find</c> in der Shell. Im chiseled-Image gibt es keine Shell mehr.</remarks>
public static class HealthCheckCommand
{
    public const string Name = "healthcheck";

    /// <summary>Wie im Auftrag: Alarm, wenn der Worker seit 15 Minuten keine Runde gespeichert hat.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    public static int Run() => Run(
        Environment.GetEnvironmentVariable("Worker__HeartbeatFile") is { Length: > 0 } file ? file : new WorkerOptions().HeartbeatFile,
        DateTimeOffset.UtcNow,
        Console.Error);

    public static int Run(string heartbeatFile, DateTimeOffset now, TextWriter error)
    {
        if (!File.Exists(heartbeatFile))
        {
            error.WriteLine($"Keine Heartbeat-Datei ({heartbeatFile}): noch keine Runde gespeichert.");
            return 1;
        }

        var age = now - File.GetLastWriteTimeUtc(heartbeatFile);
        if (age > MaxAge)
        {
            error.WriteLine($"Letzte Runde vor {age.TotalMinutes:F0} Minuten gespeichert (erlaubt: {MaxAge.TotalMinutes:F0}).");
            return 1;
        }

        return 0;
    }
}
