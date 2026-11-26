namespace PubPot.Web.Signups;

/// <summary>
/// Version des Einwilligungstextes, die mit jeder Anmeldung gespeichert wird.
/// So lässt sich später nachweisen, welchem Wortlaut jemand zugestimmt hat.
/// </summary>
/// <remarks>
/// Der Text selbst steht in Resources/SharedResource(.en).resx unter "Consent.*".
/// WICHTIG: Bei jeder Änderung am Text die Version erhöhen – ein Test (ConsentTests) erzwingt das.
/// TODO: Einwilligungstext anwaltlich prüfen lassen.
/// </remarks>
public static class ConsentText
{
    public const string Version = "2026-11-26";

    /// <summary>Alle Ressourcen-Schlüssel, die zum Einwilligungstext gehören.</summary>
    public static readonly string[] ResourceKeys = ["Consent.Text", "Consent.More", "Consent.PrivacyLink"];
}
