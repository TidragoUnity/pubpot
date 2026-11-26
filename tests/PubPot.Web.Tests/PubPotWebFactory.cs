using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
        // Ohne Datenbank: Data-Protection-Schlüssel nur im Speicher statt in der (nicht vorhandenen) DB.
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
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
        // Spam-Schutz für normale Tests entschärfen; SpamProtectionTests schalten ihn gezielt wieder scharf.
        builder.UseSetting("Spam:MinFillSeconds", "0");
        builder.UseSetting("Spam:MaxPostsPerWindow", "100000");
        builder.UseSetting("Spam:MailCooldown", "00:00:00");
    }
}
