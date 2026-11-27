using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using PubPot.Web.Data;
using PubPot.Web.Email;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>
/// Echter Versand über SMTP an Mailpit (Testcontainers). Alle anderen Tests ersetzen den Versand durch eine
/// Attrappe. Dieser prüft den Teil, der von MailKit abhängt, z. B. nach einem Update (gefunden mit MailKit 5.0).
/// </summary>
public sealed class SmtpTransportTests : IAsyncLifetime
{
    private const ushort SmtpPort = 1025;
    private const ushort ApiPort = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.34.0")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ApiPort).ForPath("/readyz")))
        .Build();

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync(Ct);

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    private SmtpEmailTransport Transport() => new(Options.Create(new SmtpOptions
    {
        Host = _mailpit.Hostname,
        Port = _mailpit.GetMappedPublicPort(SmtpPort),
        Security = SmtpSecurity.None,
        FromAddress = "pubpot@example.test",
        FromName = "PubPot",
    }));

    private Uri Api(string path) => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}/api/v1/{path}");

    [Fact]
    public async Task Sends_plain_text_mail_with_umlauts()
    {
        await Transport().SendAsync(new OutboxEmail
        {
            To = "kim@example.com",
            Subject = "Bitte bestätige deine Anmeldung – PubPot",
            TextBody = "Hallo Kim,\n\nbitte bestätige: https://pubpot.test/de/signup/confirm?token=abc\n",
        }, Ct);

        using var http = new HttpClient();
        var list = await http.GetFromJsonAsync<JsonElement>(Api("messages"), Ct);
        var summary = Assert.Single(list.GetProperty("messages").EnumerateArray());
        var id = summary.GetProperty("ID").GetString();
        var message = await http.GetFromJsonAsync<JsonElement>(Api($"message/{id}"), Ct);

        Assert.Equal("Bitte bestätige deine Anmeldung – PubPot", message.GetProperty("Subject").GetString());
        Assert.Equal("pubpot@example.test", message.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal("PubPot", message.GetProperty("From").GetProperty("Name").GetString());
        Assert.Equal("kim@example.com", Assert.Single(message.GetProperty("To").EnumerateArray()).GetProperty("Address").GetString());
        Assert.Contains("bitte bestätige: https://pubpot.test/de/signup/confirm?token=abc", message.GetProperty("Text").GetString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, message.GetProperty("HTML").GetString());
    }

    [Fact]
    public async Task Unreachable_server_throws_so_the_outbox_retries()
    {
        var transport = new SmtpEmailTransport(Options.Create(new SmtpOptions
        {
            Host = _mailpit.Hostname,
            Port = 1, // dort lauscht nichts
            Security = SmtpSecurity.None,
            FromAddress = "pubpot@example.test",
        }));

        await Assert.ThrowsAnyAsync<Exception>(() => transport.SendAsync(
            new OutboxEmail { To = "kim@example.com", Subject = "x", TextBody = "x" }, Ct));
    }
}
