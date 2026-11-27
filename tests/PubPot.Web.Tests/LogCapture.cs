using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PubPot.Web.Tests;

/// <summary>
/// Sammelt Log-Einträge der Test-App, damit Tests prüfen können, was im Log landet (Schritt 7.1).
/// Sammelt nur zwischen <see cref="Start"/> und <see cref="Stop"/>, sonst wüchse die Liste über alle Tests.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();
    private volatile bool _active;

    /// <summary>
    /// Pro Eintrag ein Text mit allem, was ein Log-Ziel zu sehen bekommt: Kategorie, fertige Nachricht,
    /// jedes einzelne Feld und die Ausnahme samt Stacktrace.
    /// </summary>
    public IReadOnlyList<string> Entries => [.. _entries];

    public void Start()
    {
        _entries.Clear();
        _active = true;
    }

    public void Stop() => _active = false;

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture capture, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => capture._active && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var fields = state is IEnumerable<KeyValuePair<string, object?>> values
                ? string.Join(" | ", values.Select(v => $"{v.Key}={v.Value}"))
                : "";
            capture._entries.Enqueue($"{logLevel} {category}: {formatter(state, exception)} [{fields}] {exception}");
        }
    }
}
