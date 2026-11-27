namespace TwitchWorker.Tests;

/// <summary>Healthcheck im Container: gesund, solange die Heartbeat-Datei höchstens 15 Minuten alt ist (E-036).</summary>
public sealed class HealthCheckCommandTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"heartbeat-test-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    public void Dispose() => File.Delete(_file);

    private int RunWithAge(TimeSpan age, StringWriter error)
    {
        File.WriteAllText(_file, "x");
        File.SetLastWriteTimeUtc(_file, (_now - age).UtcDateTime);
        return HealthCheckCommand.Run(_file, _now, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    public void Fresh_heartbeat_is_healthy(int minutes)
    {
        using var error = new StringWriter();
        Assert.Equal(0, RunWithAge(TimeSpan.FromMinutes(minutes), error));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Old_heartbeat_is_unhealthy()
    {
        using var error = new StringWriter();
        Assert.Equal(1, RunWithAge(TimeSpan.FromMinutes(16), error));
        Assert.Contains("vor 16 Minuten", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_heartbeat_is_unhealthy()
    {
        using var error = new StringWriter();
        Assert.Equal(1, HealthCheckCommand.Run(_file, _now, error));
        Assert.Contains("noch keine Runde", error.ToString(), StringComparison.Ordinal);
    }
}
