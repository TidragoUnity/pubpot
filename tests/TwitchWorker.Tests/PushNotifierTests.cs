using System.Net;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace TwitchWorker.Tests;

/// <summary>Push-URL nach vollständigen Runden (Schritt 7.3, E-052).</summary>
public sealed class PushNotifierTests
{
    private const string Url = "https://monitor.example/ping/geheimer-schluessel-123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri?> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri);
            return Task.FromResult(respond(request));
        }
    }

    private static (PushNotifier Push, FakeHandler Handler, FakeLogger<PushNotifier> Log) Create(
        string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var log = new FakeLogger<PushNotifier>();
        var push = new PushNotifier(new HttpClient(handler), Options.Create(new WorkerOptions { PushUrl = url }), log);
        return (push, handler, log);
    }

    [Fact]
    public async Task Calls_the_push_url()
    {
        var (push, handler, log) = Create(Url, _ => new HttpResponseMessage(HttpStatusCode.OK));

        await push.PushAsync(Ct);

        Assert.Equal([new Uri(Url)], handler.Requests);
        Assert.Empty(log.Collector.GetSnapshot());
    }

    [Fact]
    public async Task Without_url_nothing_is_called()
    {
        var (push, handler, _) = Create("", _ => new HttpResponseMessage(HttpStatusCode.OK));

        await push.PushAsync(Ct);

        Assert.Empty(handler.Requests);
    }

    /// <summary>Ein ausgefallener Monitor-Dienst darf den Worker nicht stoppen. Die URL bleibt aus dem Log.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failures_are_logged_without_the_url_and_do_not_throw(bool unreachable)
    {
        var (push, _, log) = Create(Url, _ => unreachable
            ? throw new HttpRequestException($"Verbindung zu {Url} fehlgeschlagen")
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await push.PushAsync(Ct);

        var entry = Assert.Single(log.Collector.GetSnapshot());
        Assert.Contains("Push-URL", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("geheimer-schluessel", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("monitor.example", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("https://monitor.example/ping/abc", true)]
    [InlineData("http://uptime.local:3001/api/push/abc", true)]
    [InlineData("monitor.example/ping", false)]
    [InlineData("ftp://monitor.example/ping", false)]
    public void Validates_the_url(string url, bool valid) => Assert.Equal(valid, PushNotifier.IsValid(url));
}
