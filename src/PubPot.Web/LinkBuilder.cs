using Microsoft.Extensions.Options;

namespace PubPot.Web;

/// <summary>
/// Baut absolute Links (für Mails, hreflang, canonical).
/// </summary>
/// <remarks>
/// Sicherheit: Links in Mails dürfen NICHT aus dem Host-Header der Anfrage entstehen. Den kann ein Angreifer frei
/// setzen ("Host: boese.example") – das Opfer bekäme dann eine echte PubPot-Mail mit einem Link auf eine fremde Seite.
/// Deshalb ist <c>Site:BaseUrl</c> außerhalb der Entwicklung Pflicht (Prüfung beim Start in Program.cs).
/// </remarks>
public sealed class LinkBuilder(IHttpContextAccessor accessor, IOptions<SiteOptions> site)
{
    public string Absolute(string path)
    {
        if (!string.IsNullOrWhiteSpace(site.Value.BaseUrl))
        {
            return site.Value.BaseUrl.TrimEnd('/') + path;
        }

        // Nur lokal (Development): aus der Anfrage ableiten.
        var request = accessor.HttpContext?.Request
            ?? throw new InvalidOperationException("Site:BaseUrl ist nicht gesetzt und es gibt keine laufende Anfrage.");
        return $"{request.Scheme}://{request.Host}{path}";
    }
}
