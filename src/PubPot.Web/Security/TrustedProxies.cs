using Microsoft.AspNetCore.HttpOverrides;

namespace PubPot.Web.Security;

/// <summary>
/// Betrieb hinter einem Reverse Proxy (Caddy, E-033). Der Proxy schreibt die echte Client-IP in
/// <c>X-Forwarded-For</c> und das ursprüngliche Protokoll in <c>X-Forwarded-Proto</c>. Die App übernimmt beides,
/// aber nur von Absendern aus <c>Proxy:KnownNetworks</c> (CIDR, kommagetrennt, z. B. <c>172.30.0.0/24</c>).
/// </summary>
/// <remarks>
/// Ohne vertrauenswürdige Netze ist die Middleware gar nicht aktiv. Das ist Absicht: ASP.NET übernimmt die Header
/// von JEDEM Absender, wenn die Listen leer sind. Dann könnte jeder Besucher eine fremde IP vortäuschen und das
/// Rate-Limiting umgehen.
/// </remarks>
public static class TrustedProxies
{
    public const string ConfigKey = "Proxy:KnownNetworks";

    /// <summary>Liest die Netze aus der Konfiguration. Ein ungültiger Eintrag bricht den Start ab (fail fast).</summary>
    public static IReadOnlyList<System.Net.IPNetwork> Parse(string? value)
    {
        var networks = new List<System.Net.IPNetwork>();
        foreach (var entry in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.Net.IPNetwork.TryParse(entry, out var network))
            {
                throw new InvalidOperationException(
                    $"{ConfigKey}: \"{entry}\" ist kein Netz in CIDR-Schreibweise (z. B. 172.30.0.0/24).");
            }

            networks.Add(network);
        }

        return networks;
    }

    /// <summary>Registriert die Einstellungen und gibt die Netze zurück (leer: Middleware nicht einschalten).</summary>
    public static IReadOnlyList<System.Net.IPNetwork> AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var networks = Parse(configuration[ConfigKey]);
        if (networks.Count == 0)
        {
            return networks;
        }

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            // Nur IP und Protokoll. Den Host nicht: Links kommen ohnehin aus Site:BaseUrl (E-021).
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Genau ein Proxy (Caddy) steht vor der App: Nur der letzte Eintrag in X-Forwarded-For zählt.
            o.ForwardLimit = 1;
            // Standardmäßig wird nur Loopback vertraut. Wir vertrauen ausschließlich den konfigurierten Netzen.
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var network in networks)
            {
                o.KnownIPNetworks.Add(network);
            }
        });
        return networks;
    }
}
