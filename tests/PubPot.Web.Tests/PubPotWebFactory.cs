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
        // Gewollte Verbindungsfehler (Test "Datenbank nicht erreichbar") nicht als Stacktrace ins Testprotokoll schreiben.
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "None");
        builder.UseSetting("Logging:LogLevel:Microsoft.Extensions.Diagnostics.HealthChecks", "None");
        // Data Protection versucht beim Start, die Schlüssel aus der (nicht vorhandenen) Datenbank zu lesen.
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.DataProtection", "None");
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
        builder.UseSetting("Cleanup:Enabled", "false");
        // Spam-Schutz für normale Tests entschärfen; SpamProtectionTests schalten ihn gezielt wieder scharf.
        builder.UseSetting("Spam:MinFillSeconds", "0");
        builder.UseSetting("Spam:MaxPostsPerWindow", "100000");
        builder.UseSetting("Spam:MailCooldown", "00:00:00");
        // Admin-Bereich an, mit bekanntem Test-Passwort. Rate-Limit für Logins entschärft (eigene Tests prüfen es).
        builder.UseSetting("Admin:PasswordHash", TestAdmin.PasswordHash);
        builder.UseSetting("Admin:LoginPostsPerWindow", "100000");
    }
}

/// <summary>Zugangsdaten des Admin-Kontos in Tests. Der Hash wird einmal pro Testlauf erzeugt.</summary>
internal static class TestAdmin
{
    public const string Username = "admin";

    public const string Password = "test-passwort-fuer-admin";

    public static readonly string PasswordHash = PubPot.Web.Admin.AdminPassword.Hash(Password);
}
