using System.Collections.Concurrent;
using PubPot.Web.Data;
using PubPot.Web.Email;

namespace PubPot.Web.Tests;

/// <summary>Mailversand im Speicher: merkt sich alle "verschickten" Mails; kann einen Ausfall simulieren.</summary>
public sealed class FakeEmailTransport : IEmailTransport
{
    private readonly ConcurrentQueue<OutboxEmail> _sent = new();

    public IReadOnlyList<OutboxEmail> Sent => [.. _sent];

    /// <summary>Wenn true, schlägt jeder Versand fehl (z. B. Mailserver nicht erreichbar).</summary>
    public bool Fail { get; set; }

    public Task SendAsync(OutboxEmail email, CancellationToken ct)
    {
        if (Fail)
        {
            throw new IOException("Mailserver nicht erreichbar (simuliert).");
        }

        _sent.Enqueue(email);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sent.Clear();
        Fail = false;
    }
}
