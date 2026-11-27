using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PubPot.Web.Data;

/// <summary>
/// Erzeugt den <see cref="AppDbContext"/> für <c>dotnet ef</c> und das Migrations-Bundle (E-035).
/// </summary>
/// <remarks>
/// Ohne diese Klasse startet EF dafür die ganze App bis kurz vor dem Webserver. Dann bräuchte die Migration auch
/// SMTP, <c>Site:BaseUrl</c> und alle anderen Pflichtwerte. So braucht sie nur den Connection-String:
/// lokal aus den User Secrets, im Container aus <c>ConnectionStrings__Default</c>.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => Create(
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(AppDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build());

    public static AppDbContext Create(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default fehlt. Lokal: dotnet user-secrets (siehe README), im Container: ConnectionStrings__Default.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }
}
