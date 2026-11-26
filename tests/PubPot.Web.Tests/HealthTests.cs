using System.Net;
using Npgsql;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>/health (Liveness) und /health/ready (Readiness) mit echter Datenbank.</summary>
[Collection("Database")]
public class HealthTests(DatabaseWebFactory factory)
{
    [Fact]
    public async Task Ready_when_database_is_reachable_and_migrated()
    {
        using var response = await GetAsync(CreateClient(factory), "/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Not_ready_when_migrations_are_missing()
    {
        // Leere Datenbank im selben Container: erreichbar, aber ohne Tabellen.
        var builder = new NpgsqlConnectionStringBuilder(factory.ConnectionString);
        var empty = "empty_" + Guid.NewGuid().ToString("N")[..8];
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand($"CREATE DATABASE {empty}", connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        builder.Database = empty;
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", builder.ConnectionString));
        using var response = await GetAsync(CreateClient(app), "/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Not_ready_but_alive_when_database_is_unreachable()
    {
        // PubPotWebFactory zeigt auf eine Datenbank, die es nicht gibt.
        await using var noDatabase = new PubPotWebFactory();
        var client = CreateClient(noDatabase);

        using var ready = await GetAsync(client, "/health/ready");
        using var live = await GetAsync(client, "/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode); // Prozess lebt weiter → kein Neustart-Karussell
    }
}
