namespace PubPot.Web.Localization;

/// <summary>
/// Routen-Einschränkung <c>{culture:culture}</c>: passt nur auf unterstützte Sprachen.
/// So landet z. B. <c>/fr</c> sauber auf der 404-Seite, statt eine Seite in falscher Sprache zu zeigen.
/// Nur Kleinschreibung: <c>/DE</c> wäre sonst ein Duplikat von <c>/de</c> (schlecht für Suchmaschinen).
/// </summary>
public sealed class CultureRouteConstraint : IRouteConstraint
{
    public const string Name = "culture";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value) && value is string culture
        && SupportedCultures.All.Contains(culture, StringComparer.Ordinal);
}
