using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PubPot.Web.Data;
using Testcontainers.PostgreSql;

namespace PubPot.Web.Tests;

/// <summary>
/// Startet die Web-App gegen ein echtes PostgreSQL 16 in Docker (Testcontainers).
/// Der Container läuft einmal pro Testlauf, Testcontainers räumt ihn danach automatisch weg.
/// Vor jedem Test leert <see cref="ResetAsync"/> die Tabellen, damit Tests sich nicht gegenseitig beeinflussen.
/// </summary>
public sealed class DatabaseWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.16").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Schema wie in Produktion über die EF-Migrationen anlegen (nicht EnsureCreated),
        // damit die Tests auch die Migrationen selbst prüfen.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

    /// <summary>Leert alle Tabellen (schneller als die Datenbank neu anzulegen).</summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE signups, signup_confirmations, outbox_emails CASCADE");
    }

    /// <summary>Führt Code mit einem frischen DbContext aus, z. B. um in Tests die Datenbank zu prüfen.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Alle Test-Klassen mit [Collection("Database")] teilen sich einen Postgres-Container.</summary>
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseWebFactory>;
