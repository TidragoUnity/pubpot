using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PubPot.Web.Forms;

/// <summary>
/// Barrierefreie Fehlermeldungen: Hat ein Feld einen Fehler, bekommt es <c>aria-invalid="true"</c> und
/// <c>aria-describedby</c> zeigt auf die Fehlermeldung. Screenreader lesen den Fehler dann beim Feld vor.
/// Gilt automatisch für alle input/select/textarea mit asp-for.
/// </summary>
[HtmlTargetElement("input", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "asp-for")]
[HtmlTargetElement("textarea", Attributes = "asp-for")]
public sealed class AriaInvalidTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    // Nach den eingebauten Tag-Helpern ausführen (die setzen id und name).
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (!ViewContext.ViewData.ModelState.TryGetValue(name, out var entry) || entry.Errors.Count == 0)
        {
            return;
        }

        output.Attributes.SetAttribute("aria-invalid", "true");
        var errorId = FieldErrorTagHelper.ErrorId(name);
        var describedBy = output.Attributes.TryGetAttribute("aria-describedby", out var existing)
            ? $"{existing.Value} {errorId}"
            : errorId;
        output.Attributes.SetAttribute("aria-describedby", describedBy);
    }
}

/// <summary>
/// <c>&lt;field-error for="Input.Name" /&gt;</c> → zeigt die erste Fehlermeldung des Feldes an, sonst nichts.
/// Die id passt zu <see cref="AriaInvalidTagHelper"/>.
/// </summary>
[HtmlTargetElement("field-error", TagStructure = TagStructure.WithoutEndTag)]
public sealed class FieldErrorTagHelper : TagHelper
{
    public ModelExpression For { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public static string ErrorId(string fullName) => TagBuilder.CreateSanitizedId(fullName, "_") + "-error";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (!ViewContext.ViewData.ModelState.TryGetValue(name, out var entry) || entry.Errors.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "p";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", ErrorId(name));
        output.Attributes.SetAttribute("class", "field-error");
        output.Content.SetContent(entry.Errors[0].ErrorMessage);
    }
}
