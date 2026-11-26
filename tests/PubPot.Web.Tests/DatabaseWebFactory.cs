using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PubPot.Web.Data;
using PubPot.Web.Email;
using Testcontainers.PostgreSql;

namespace PubPot.Web.Tests;

/// <summary>
/// Startet die Web-App gegen ein echtes PostgreSQL 16 in Docker (Testcontainers).
/// Der Container läuft einmal pro Testlauf, Testcontainers räumt ihn danach automatisch weg.
/// Vor jedem Test leert <see cref="ResetAsync"/> die Tabellen, damit Tests sich nicht gegenseitig beeinflussen.
/// Mails landen in <see cref="Mail"/> statt bei einem echten Mailserver.
/// </summary>
public sealed class DatabaseWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.16").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public FakeEmailTransport Mail { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Schema wie in Produktion über die EF-Migrationen anlegen (nicht EnsureCreated),
        // damit die Tests auch die Migrationen selbst prüfen.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        TestSettings.Apply(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailTransport>(Mail));
    }

    /// <summary>Leert alle Tabellen und den Fake-Mailversand.</summary>
    public async Task ResetAsync()
    {
        Mail.Reset();
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

    /// <summary>Holt einen Dienst aus einem frischen Scope (z. B. den OutboxProcessor) und führt Code damit aus.</summary>
    public async Task<T> WithServiceAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
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
