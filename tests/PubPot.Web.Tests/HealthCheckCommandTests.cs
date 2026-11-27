using System.Net;

namespace PubPot.Web.Tests;

/// <summary>Der Befehl <c>healthcheck</c>, den Docker im Container aufruft (E-034).</summary>
public class HealthCheckCommandTests(PubPotWebFactory factory) : IClassFixture<PubPotWebFactory>
{
    [Theory]
    [InlineData(null, 8080)]
    [InlineData("", 8080)]
    [InlineData("5000", 5000)]
    [InlineData("8080;8081", 8080)]
    [InlineData("kaputt", 8080)]
    [InlineData("70000", 8080)]
    public void Uses_first_kestrel_port(string? httpPorts, int expected) =>
        Assert.Equal(new Uri($"http://127.0.0.1:{expected}/health"), HealthCheckCommand.LocalUri(httpPorts));

    [Fact]
    public async Task Healthy_app_gives_exit_code_0()
    {
        using var client = factory.CreateClient();
        using var error = new StringWriter();

        var exitCode = await HealthCheckCommand.RunAsync(client, new Uri(client.BaseAddress!, HealthCheckCommand.Path), error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Error_status_gives_exit_code_1()
    {
        using var client = new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var error = new StringWriter();

        Assert.Equal(1, await HealthCheckCommand.RunAsync(client, new Uri("http://127.0.0.1:8080/health"), error));
        Assert.Contains("503", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unreachable_app_gives_exit_code_1()
    {
        using var client = new HttpClient(new Stub(_ => throw new HttpRequestException("Connection refused")));
        using var error = new StringWriter();

        Assert.Equal(1, await HealthCheckCommand.RunAsync(client, new Uri("http://127.0.0.1:8080/health"), error));
        Assert.Contains("nicht erreichbar", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
