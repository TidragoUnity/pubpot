using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace PubPot.Web.Localization;

/// <summary>
/// Setzt vor jede Razor Page das Sprachpräfix: aus <c>/Signup/Developer</c> wird <c>/{culture}/signup/developer</c>.
/// Ausgenommen sind die sprachneutralen Seiten unter <c>Pages/Status</c> (Fehler, 404) und der
/// Admin-Bereich unter <c>Pages/Admin</c>, den es nur auf Deutsch gibt (E-024).
/// </summary>
public sealed class CultureRouteConvention : IPageRouteModelConvention
{
    public const string RouteKey = "culture";

    public void Apply(PageRouteModel model)
    {
        if (model.ViewEnginePath.StartsWith("/Status/", StringComparison.Ordinal)
            || model.ViewEnginePath.StartsWith("/Admin/", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var selector in model.Selectors)
        {
            var route = selector.AttributeRouteModel;
            if (route is null)
            {
                continue;
            }

            route.Template = AttributeRouteModel.CombineTemplates(
                $"{{{RouteKey}:{CultureRouteConstraint.Name}}}", route.Template);
        }
    }
}
