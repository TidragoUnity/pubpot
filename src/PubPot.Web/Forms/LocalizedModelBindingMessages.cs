using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace PubPot.Web.Forms;

/// <summary>
/// Fehlermeldungen der Modellbindung (z. B. "The value 'x' is not valid") gibt es sonst nur auf Englisch.
/// Hier werden sie durch eine übersetzte, allgemeine Meldung ersetzt.
/// </summary>
public sealed class LocalizedModelBindingMessages(IStringLocalizer<SharedResource> text) : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;
        string Invalid() => text["Validation.Invalid"];

        messages.SetAttemptedValueIsInvalidAccessor((_, _) => Invalid());
        messages.SetUnknownValueIsInvalidAccessor(_ => Invalid());
        messages.SetValueIsInvalidAccessor(_ => Invalid());
        messages.SetValueMustBeANumberAccessor(_ => Invalid());
        messages.SetValueMustNotBeNullAccessor(_ => text["Validation.Required"]);
        messages.SetMissingBindRequiredValueAccessor(_ => text["Validation.Required"]);
        messages.SetMissingKeyOrValueAccessor(() => text["Validation.Required"]);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => Invalid());
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(Invalid);
        messages.SetNonPropertyValueMustBeANumberAccessor(Invalid);
    }
}
