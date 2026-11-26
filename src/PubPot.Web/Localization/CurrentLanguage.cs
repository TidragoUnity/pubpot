using System.Globalization;

namespace PubPot.Web.Localization;

/// <summary>
/// Sprache der aktuellen Anfrage und die daraus abgeleiteten Links (Umschalter, hreflang, canonical).
/// In allen Views als <c>Lang</c> verfügbar (siehe _ViewImports.cshtml).
/// </summary>
public sealed class CurrentLanguage(IHttpContextAccessor accessor, LinkBuilder links)
{
    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("CurrentLanguage wird nur während einer Anfrage verwendet.");

    /// <summary>Hat die Seite ein Sprachpräfix (<c>/de/...</c>)? Fehlerseiten haben keins.</summary>
    public bool IsLocalizedPage => Context.GetRouteValue(CultureRouteConvention.RouteKey) is string;

    /// <summary><c>de</c> oder <c>en</c>.</summary>
    public string Code =>
        Context.GetRouteValue(CultureRouteConvention.RouteKey) is string route && SupportedCultures.IsSupported(route)
            ? route.ToLowerInvariant()
            : SupportedCultures.IsSupported(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
                ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                : SupportedCultures.Default;

    public string Other => SupportedCultures.Other(Code);

    /// <summary>Link für den Sprachumschalter: setzt das Sprach-Cookie und führt zur selben Seite in der anderen Sprache.</summary>
    public string SwitchUrl(string target) =>
        $"/lang/{target}?returnPath={Uri.EscapeDataString(Context.Request.Path.Value ?? "/")}";

    /// <summary>
    /// Sprachvorschlag beim ersten Besuch: Hat der Besucher noch nichts gewählt und bevorzugt sein Browser
    /// die andere Sprache, wird diese zurückgegeben, sonst <c>null</c>.
    /// </summary>
    public string? Suggestion
    {
        get
        {
            if (!IsLocalizedPage || LanguagePreference.FromCookie(Context) is not null)
            {
                return null;
            }

            var browser = LanguagePreference.FromBrowser(Context);
            return browser is not null && browser != Code ? browser : null;
        }
    }

    /// <summary>Absolute URL derselben Seite in der angegebenen Sprache (für hreflang).</summary>
    public string AlternateUrl(string culture) =>
        Absolute(LanguagePreference.TranslatePath(Context.Request.Path.Value, culture));

    /// <summary>Absolute URL der aktuellen Seite (für canonical).</summary>
    public string CanonicalUrl => Absolute(Context.Request.Path.Value ?? "/");

    /// <summary>Für hreflang="x-default": die Wurzel, die nach Browsersprache weiterleitet.</summary>
    public string DefaultUrl => Absolute("/");

    private string Absolute(string path) => links.Absolute(path);

    /// <summary>Führt <paramref name="action"/> mit einer anderen UI-Sprache aus (z. B. für den Sprachvorschlag).</summary>
    public static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
