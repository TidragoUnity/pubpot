using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Im Container schreiben Web und Worker eine JSON-Zeile pro Eintrag (Schritt 7.1, E-050). Der Test baut die
/// Konsolen-Ausgabe mit den echten appsettings-Dateien auf und schreibt einen Eintrag wie die App.
/// </summary>
public sealed partial class LogFormatTests
{
    private static ServiceProvider ConsoleLogging(params string[] files)
    {
        var config = new ConfigurationBuilder();
        foreach (var file in files)
        {
            config.AddJsonFile(Path.Combine(RepoRoot, file), optional: false);
        }

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConfiguration(config.Build().GetSection("Logging")).AddConsole());
        return services.BuildServiceProvider();
    }

    private static string Write(ServiceProvider provider, Exception? exception = null)
    {
        var name = provider.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue.FormatterName;
        var formatter = provider.GetServices<ConsoleFormatter>().Single(f => f.Name == name);

        // Aufbau wie bei log.LogInformation("Anmeldung {SignupId} bestätigt.", id)
        IReadOnlyList<KeyValuePair<string, object?>> state =
        [
            new("SignupId", 42),
            new("{OriginalFormat}", "Anmeldung {SignupId} bestätigt."),
        ];
        using var writer = new StringWriter();
        formatter.Write(
            new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
                LogLevel.Information, "PubPot.Web.Signups.SignupService", new EventId(0), state, exception,
                (_, _) => "Anmeldung 42 bestätigt."),
            new LoggerExternalScopeProvider(), writer);
        return writer.ToString();
    }

    [Theory]
    [InlineData("src/PubPot.Web/appsettings.json")]
    [InlineData("src/TwitchWorker/appsettings.json")]
    public void Container_writes_one_json_line_per_entry_with_utc_time(string appsettings)
    {
        using var provider = ConsoleLogging(appsettings);

        var output = Write(provider);

        Assert.EndsWith(Environment.NewLine, output, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', output.TrimEnd());
        using var json = JsonDocument.Parse(output);
        var root = json.RootElement;
        Assert.Matches(UtcTimestamp(), root.GetProperty("Timestamp").GetString());
        Assert.Equal("Information", root.GetProperty("LogLevel").GetString());
        Assert.Equal("PubPot.Web.Signups.SignupService", root.GetProperty("Category").GetString());
        Assert.Equal("Anmeldung 42 bestätigt.", root.GetProperty("Message").GetString());
        // Einzelne Felder bleiben erhalten, z. B. zum Filtern nach SignupId.
        Assert.Equal(42, root.GetProperty("State").GetProperty("SignupId").GetInt32());
        // Scopes (Anfragepfad, Verbindungs-ID) bleiben draußen.
        Assert.False(root.TryGetProperty("Scopes", out _));
    }

    /// <summary>Ein Stacktrace hat viele Zeilen. Als JSON bleibt er ein Eintrag und zerfällt nicht in Bruchstücke.</summary>
    [Fact]
    public void Exception_with_stack_trace_stays_on_one_line()
    {
        using var provider = ConsoleLogging("src/PubPot.Web/appsettings.json");
        Exception exception;
        try
        {
            throw new InvalidOperationException("Zeile 1\nZeile 2");
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        var output = Write(provider, exception);

        Assert.DoesNotContain('\n', output.TrimEnd());
        using var json = JsonDocument.Parse(output);
        Assert.Contains("Zeile 1\nZeile 2", json.RootElement.GetProperty("Exception").GetString(), StringComparison.Ordinal);
    }

    /// <summary>Bei <c>dotnet run</c> (Development) bleibt die gut lesbare Ausgabe mit Uhrzeit.</summary>
    [Fact]
    public void Development_keeps_readable_console_output()
    {
        using var provider = ConsoleLogging("src/PubPot.Web/appsettings.json", "src/PubPot.Web/appsettings.Development.json");

        var output = Write(provider);

        // Ohne feste Prüfung auf "info": Je nach Terminal steht die Stufe in Farbcodes.
        Assert.Matches(@"^\d\d:\d\d:\d\d ", output);
        Assert.Contains("PubPot.Web.Signups.SignupService[0]", output, StringComparison.Ordinal);
        Assert.Contains("Anmeldung 42 bestätigt.", output, StringComparison.Ordinal);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$")]
    private static partial System.Text.RegularExpressions.Regex UtcTimestamp();
}
