using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PubPot.Web.Data;
using PubPot.Web.Notifications;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Hinweis ans Team bei neuer bestätigter Anmeldung: Mail über die Outbox, optional Discord (Schritt 4.8, E-030).</summary>
[Collection("Database")]
public partial class NotificationTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private const string TeamEmail = "team@example.com";
    private const string Webhook = "https://discord.com/api/webhooks/123456/geheimes-token";

    private readonly FakeDiscord _discord = new();
    private readonly LogCapture _logs = new();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private WebApplicationFactory<Program> App(string email = TeamEmail, string webhook = "") =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Notifications:Email", email);
            b.UseSetting("Notifications:DiscordWebhookUrl", webhook);
            b.ConfigureLogging(l => l.AddProvider(_logs));
            b.ConfigureTestServices(s => s.AddHttpClient(DiscordSenderService.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _discord));
        });

    /// <summary>Anmelden wie ein Besucher, dann den Link aus der Bestätigungsmail anklicken.</summary>
    private static async Task<Guid> SignUpAndConfirmAsync(WebApplicationFactory<Program> app, string genres = "Roguelikes")
    {
        var client = CreateClient(app);
        using var signup = await PostFormAsync(client, "/de/signup/creator",
        [
            new("Input.Name", "Kim Geheim"), new("Input.Email", "kim.geheim@example.com"), new("Input.Platforms", "Twitch"),
            new("Input.Channel", "kim_geheim_kanal"), new("Input.AudienceSize", "Under10"), new("Input.Genres", genres),
            new("Input.Adult", "true"), new("Input.Consent", "true"),
        ]);
        Assert.Equal(HttpStatusCode.Redirect, signup.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mail = await db.OutboxEmails.Where(m => m.To == "kim.geheim@example.com").OrderByDescending(m => m.CreatedAt).FirstAsync(Ct);
        var token = Token().Match(mail.TextBody).Groups[1].Value;

        using var confirm = await PostFormAsync(client, "/de/signup/confirm", [new("Token", token)], tokenSource: "/de/signup/creator");
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        return await db.Signups.Select(s => s.Id).SingleAsync(Ct);
    }

    private Task<List<OutboxEmail>> TeamMailsAsync() =>
        factory.WithDbAsync(db => db.OutboxEmails.Where(m => m.To == TeamEmail).ToListAsync(Ct));

    [Fact]
    public async Task Confirmed_signup_queues_a_team_mail_without_personal_data()
    {
        await using var app = App();

        var id = await SignUpAndConfirmAsync(app);

        var mail = Assert.Single(await TeamMailsAsync());
        Assert.Equal("PubPot: neue bestätigte Anmeldung (Creator)", mail.Subject);
        Assert.Contains("Art: Creator", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("Sprache: Deutsch", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains($"http://localhost/admin/signups/{id}", mail.TextBody, StringComparison.Ordinal);
        foreach (var personal in new[] { "Kim", "Geheim", "kim.geheim@example.com", "kim_geheim_kanal" })
        {
            Assert.DoesNotContain(personal, mail.Subject + mail.TextBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Confirming_a_change_of_a_confirmed_signup_sends_no_new_team_mail()
    {
        await using var app = App();
        await SignUpAndConfirmAsync(app);

        await SignUpAndConfirmAsync(app, genres: "Cozy"); // gleiche Adresse: Änderung, nicht neu

        Assert.Single(await TeamMailsAsync());
        Assert.Equal("Cozy", await factory.WithDbAsync(db => db.Signups.OfType<CreatorSignup>().Select(s => s.Genres).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Without_team_address_no_team_mail_is_queued()
    {
        await using var app = App(email: "");

        await SignUpAndConfirmAsync(app);

        Assert.Equal(1, await factory.WithDbAsync(db => db.OutboxEmails.CountAsync(Ct))); // nur die Bestätigungsmail
        Assert.Empty(_discord.Bodies);
    }

    [Fact]
    public async Task Discord_gets_a_short_message_without_personal_data_and_without_mentions()
    {
        await using var app = App(webhook: Webhook);

        var id = await SignUpAndConfirmAsync(app);
        var body = await _discord.WaitForSuccessAsync();

        Assert.Equal(new Uri(Webhook), _discord.LastUri);
        Assert.Contains("Neue bestätigte Anmeldung: Creator (DE)", Regex.Unescape(body), StringComparison.Ordinal);
        Assert.Contains($"/admin/signups/{id}", body, StringComparison.Ordinal);
        Assert.Contains("\"allowed_mentions\":{\"parse\":[]}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Kim", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Discord_is_retried_after_an_error_and_the_webhook_url_never_appears_in_logs()
    {
        _discord.Responses.Enqueue(HttpStatusCode.InternalServerError);
        await using var app = App(webhook: Webhook);

        await SignUpAndConfirmAsync(app);
        await _discord.WaitForSuccessAsync();

        Assert.Equal(2, _discord.Bodies.Count);
        Assert.Contains(_logs.Messages, m => m.Contains("abgelehnt (HTTP 500)", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Messages, m => m.Contains("geheimes-token", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Messages, m => m.Contains("api/webhooks", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Notifications:Email", "keine-adresse")]
    [InlineData("Notifications:DiscordWebhookUrl", "http://discord.com/api/webhooks/1/x")] // kein HTTPS
    [InlineData("Notifications:DiscordWebhookUrl", "https://boese.example/api/webhooks/1/x")] // fremder Host
    [InlineData("Notifications:DiscordWebhookUrl", "https://discord.com/channels/1")] // kein Webhook
    public void Broken_notification_settings_stop_the_start(string key, string value)
    {
        using var app = new PubPotWebFactory().WithWebHostBuilder(b => b.UseSetting(key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => app.Services);
        Assert.Contains("Notifications:", exception.Message, StringComparison.Ordinal);
    }

    [GeneratedRegex("token=([A-Za-z0-9_-]{43})")]
    private static partial Regex Token();

    /// <summary>Spielt den Discord-Server: merkt sich Anfragen und antwortet wie vorgegeben (Standard: 204).</summary>
    private sealed class FakeDiscord : HttpMessageHandler
    {
        private readonly TaskCompletionSource<string> _success = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<HttpStatusCode> Responses { get; } = new();

        public ConcurrentQueue<string> Bodies { get; } = new();

        public Uri? LastUri { get; private set; }

        public async Task<string> WaitForSuccessAsync() => await _success.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Enqueue(body);
            LastUri = request.RequestUri;
            var status = Responses.TryDequeue(out var next) ? next : HttpStatusCode.NoContent;
            if ((int)status < 300)
            {
                _success.TrySetResult(body);
            }

            return new HttpResponseMessage(status);
        }
    }

    /// <summary>Sammelt alle Log-Meldungen der App, um zu prüfen, was (nicht) im Log steht.</summary>
    private sealed class LogCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(LogCapture owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Messages.Enqueue($"{category}: {formatter(state, exception)}");
        }
    }
}
