using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PubPot.Web.Localization;

namespace PubPot.Web.Spam;

/// <summary>
/// Gemeinsame Basis der Anmeldeformulare: Honeypot und Mindestausfüllzeit.
/// </summary>
public abstract class SignupFormPageModel(
    FormTiming timing,
    IOptions<SpamOptions> spam,
    CurrentLanguage language,
    IStringLocalizer<SharedResource> text,
    ILogger logger) : PageModel
{
    /// <summary>Honeypot: für Menschen unsichtbar, Bots füllen es gern aus. Muss leer bleiben.</summary>
    [BindProperty(Name = "Website")]
    public string? Honeypot { get; set; }

    /// <summary>Signierter Zeitstempel vom Anzeigen des Formulars (siehe <see cref="FormTiming"/>).</summary>
    [BindProperty]
    public string? FormStarted { get; set; }

    protected CurrentLanguage Language => language;

    protected IStringLocalizer<SharedResource> Text => text;

    /// <summary>Beim Anzeigen (GET und nach Validierungsfehlern) einen frischen Zeitstempel ausgeben.</summary>
    protected void IssueFormToken() => FormStarted = timing.Issue();

    /// <summary>
    /// Prüft Honeypot und Ausfüllzeit. Gibt bei Spam eine Weiterleitung zur Danke-Seite zurück – der Bot soll
    /// nicht merken, dass er erkannt wurde. Bei fehlendem/kaputtem Zeitstempel (z. B. Formular sehr lange offen
    /// nach einem Schlüsselwechsel) gibt es stattdessen einen Hinweis zum erneuten Absenden.
    /// </summary>
    protected IActionResult? RejectSpam()
    {
        if (!string.IsNullOrEmpty(Honeypot))
        {
            return SilentlyDrop("Honeypot ausgefüllt");
        }

        switch (timing.Check(FormStarted, spam.Value.MinFillSeconds))
        {
            case FormTimingResult.TooFast:
                return SilentlyDrop("zu schnell ausgefüllt");
            case FormTimingResult.Invalid:
                ModelState.AddModelError(string.Empty, text["Validation.FormExpired"]);
                break;
        }

        return null;
    }

    /// <summary>Danke-Seite nach erfolgreicher Anmeldung.</summary>
    protected IActionResult RedirectToThanks() => RedirectToPage("/Signup/Thanks", new { culture = language.Code });

    private RedirectToPageResult SilentlyDrop(string reason)
    {
        // Ohne IP und ohne Formulardaten loggen (personenbezogen) – nur, dass und warum verworfen wurde.
        logger.LogInformation("Anmeldung als Spam verworfen: {Reason}.", reason);
        return (RedirectToPageResult)RedirectToThanks();
    }
}
