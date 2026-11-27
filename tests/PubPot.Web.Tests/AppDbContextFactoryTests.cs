using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PubPot.Web.Data;

namespace PubPot.Web.Tests;

/// <summary>Der DbContext für <c>dotnet ef</c> und das Migrations-Bundle (E-035): braucht nur den Connection-String.</summary>
public class AppDbContextFactoryTests
{
    private static IConfiguration Config(string? connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
        .Build();

    [Fact]
    public void Uses_connection_string_and_disables_gss()
    {
        using var db = AppDbContextFactory.Create(Config("Host=db;Database=pubpot;Username=pubpot;Password=geheim"));

        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.Equal("db", connection.Host);
        Assert.Equal("pubpot", connection.Database);
        Assert.Equal(GssEncryptionMode.Disable, connection.GssEncryptionMode);
    }

    /// <summary>Gleiche Einstellungen wie die App, sonst würden Migrationen andere Tabellennamen erwarten.</summary>
    [Fact]
    public void Uses_snake_case_like_the_app()
    {
        using var db = AppDbContextFactory.Create(Config("Host=db"));

        Assert.Equal("signup_confirmations", db.Model.FindEntityType(typeof(SignupConfirmation))!.GetTableName());
        Assert.Contains(db.Database.GetMigrations(), m => m.EndsWith("_Initial", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_connection_string_explains_what_to_set(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AppDbContextFactory.Create(Config(value)));
        Assert.Contains("ConnectionStrings__Default", error.Message, StringComparison.Ordinal);
    }
}
