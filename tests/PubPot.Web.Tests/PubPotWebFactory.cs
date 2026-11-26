using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PubPot.Web.Tests;

/// <summary>
/// Startet die Web-App für Tests, die keine Datenbank brauchen (Seiten, Header, Sprachen).
/// Die Verbindungszeichenfolge zeigt ins Leere – die App startet, verbindet sich aber nie.
/// Tests mit echter Datenbank bekommen in Schritt 3.3 eine eigene Factory mit Testcontainers.
/// </summary>
public class PubPotWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", "Host=unused.invalid;Database=unused;Username=unused;Password=unused");
}
