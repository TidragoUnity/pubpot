using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PubPot.Web.Tests;

/// <summary>
/// Startet die Web-App für Tests, die keine Datenbank brauchen (Seiten, Header, Sprachen).
/// Die Verbindungszeichenfolge zeigt ins Leere – die App startet, verbindet sich aber nie.
/// Tests mit echter Datenbank nutzen <see cref="DatabaseWebFactory"/>.
/// </summary>
public class PubPotWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Host=unused.invalid;Database=unused;Username=unused;Password=unused");
        TestSettings.Apply(builder);
    }
}

/// <summary>Einstellungen, die alle Test-Instanzen brauchen.</summary>
internal static class TestSettings
{
    public static void Apply(IWebHostBuilder builder)
    {
        builder.UseSetting("Smtp:Host", "smtp.invalid");
        builder.UseSetting("Smtp:FromAddress", "pubpot@example.test");
        // Kein Hintergrundversand im Test – Tests stoßen den Mail-Ausgang selbst an.
        builder.UseSetting("Outbox:Enabled", "false");
    }
}
