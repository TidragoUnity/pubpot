using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace PubPot.Web.Forms;

/// <summary>
/// ASP.NET Core übersetzt ErrorMessage nur für Attribute, für die es einen "Adapter" kennt
/// (Required, StringLength …). Eigene Attribute würden ihren Schlüssel unübersetzt anzeigen
/// ("Validation.Consent"). Dieser Provider liefert für unsere Attribute einen Adapter, der übersetzt.
/// </summary>
public sealed class LocalizedValidationAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _builtIn = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer) =>
        attribute is EmailAttribute or MustBeTrueAttribute
            ? new ServerOnlyAdapter(attribute, stringLocalizer)
            : _builtIn.GetAttributeAdapter(attribute, stringLocalizer);

    /// <summary>Übersetzt die Meldung; keine Client-Validierung (wir nutzen kein jQuery-Validation).</summary>
    private sealed class ServerOnlyAdapter(ValidationAttribute attribute, IStringLocalizer? localizer)
        : AttributeAdapterBase<ValidationAttribute>(attribute, localizer)
    {
        public override void AddValidation(ClientModelValidationContext context)
        {
        }

        public override string GetErrorMessage(ModelValidationContextBase validationContext) =>
            GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}
