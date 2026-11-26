namespace PubPot.Web.Tests;

/// <summary>Uhr zum Vorstellen in Tests (Sperrfristen, Sitzungsdauer, Aufbewahrungsfristen).</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = start;

    public TestClock()
        : this(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_lock)
        {
            _now += by;
        }
    }
}
