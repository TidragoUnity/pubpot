namespace PubPot.Web.Localization;

/// <summary>
/// Welche Sprache will der Besucher? Reihenfolge: ausdrücklich gewählt (Cookie) → Browsersprache → Deutsch.
/// </summary>
/// <remarks>
/// Das Cookie wird nur gesetzt, wenn jemand den Sprachumschalter anklickt. Es ist damit für eine vom Nutzer
/// ausdrücklich gewünschte Funktion unbedingt erforderlich und braucht keine Einwilligung (§ 25 Abs. 2 TDDDG).
/// TODO: In der Datenschutzerklärung erwähnen (anwaltlich prüfen lassen).
/// </remarks>
public static class LanguagePreference
{
    public const string CookieName = "pubpot_lang";

    /// <summary>Sprache für die Weiterleitung von <c>/</c>.</summary>
    public static string Resolve(HttpContext context) =>
        FromCookie(context) ?? FromBrowser(context) ?? SupportedCultures.Default;

    /// <summary>Die vom Besucher ausdrücklich gewählte Sprache, sonst <c>null</c>.</summary>
    public static string? FromCookie(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var value) && SupportedCultures.IsSupported(value)
            ? value.ToLowerInvariant()
            : null;

    /// <summary>Die bevorzugte unterstützte Sprache laut <c>Accept-Language</c>, sonst <c>null</c>.</summary>
    public static string? FromBrowser(HttpContext context)
    {
        IList<Microsoft.Net.Http.Headers.StringWithQualityHeaderValue> languages;
        try
        {
            languages = context.Request.GetTypedHeaders().AcceptLanguage;
        }
        catch (FormatException)
        {
            return null; // kaputter Header: einfach ignorieren
        }

        return languages
            .Where(l => l.Quality is null or > 0)
            .OrderByDescending(l => l.Quality ?? 1)
            .Select(l => l.Value.Value?.Split('-')[0].ToLowerInvariant())
            .FirstOrDefault(SupportedCultures.IsSupported);
    }

    /// <summary>Merkt sich die gewählte Sprache für ein Jahr.</summary>
    public static void Remember(HttpContext context, string culture) =>
        context.Response.Cookies.Append(CookieName, culture.ToLowerInvariant(), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365),
            Path = "/",
        });

    /// <summary>
    /// Übersetzt einen lokalen Pfad in die Zielsprache: <c>/de/signup/creator</c> → <c>/en/signup/creator</c>.
    /// Fremde oder ungültige Pfade führen auf die Startseite der Zielsprache (Schutz vor Open Redirect).
    /// </summary>
    public static string TranslatePath(string? path, string culture)
    {
        var home = "/" + culture;
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal)
            || path.StartsWith("/\\", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
        {
            return home;
        }

        var segments = path.Split('/', 3); // ["", "de", "rest"]
        if (!SupportedCultures.IsSupported(segments[1]))
        {
            return home;
        }

        return segments.Length == 3 ? $"{home}/{segments[2]}" : home;
    }
}
