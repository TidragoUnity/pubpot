namespace PubPot.Web;

/// <summary>Allgemeine Einstellungen der Website (Abschnitt "Site", per Umgebungsvariable z. B. <c>Site__BaseUrl</c>).</summary>
public sealed class SiteOptions
{
    /// <summary>
    /// Öffentliche Adresse ohne abschließenden Schrägstrich, z. B. <c>https://pubpot.games</c>.
    /// Wird für absolute Links (hreflang, canonical) verwendet. Leer = aus der Anfrage ableiten.
    /// </summary>
    public string BaseUrl { get; set; } = "";
}
