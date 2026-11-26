using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PubPot.Web.Localization;
using PubPot.Web.Signups;
using PubPot.Web.Spam;

namespace PubPot.Web.Pages.Signup;

/// <summary>
/// „Ich möchte dabei bleiben“: Link aus der Warnmail vor dem Löschen (E-023, E-031).
/// Wie beim Bestätigungslink prüft GET nur und zeigt einen Button. Erst der Klick (POST) zählt, damit
/// Mail-Scanner, die Links vorab öffnen, die Löschung nicht unbemerkt verhindern.
/// </summary>
[EnableRateLimiting(RateLimits.FormPosts)]
public sealed class KeepModel(SignupRetention retention, CurrentLanguage language) : PageModel
{
    public enum View
    {
        Prompt,
        Kept,
        Invalid,
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public View Result { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Result = Status switch
        {
            "kept" => View.Kept,
            "invalid" => View.Invalid,
            _ => await retention.CheckAsync(Token, ct) ? View.Prompt : View.Invalid,
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var status = await retention.KeepAsync(Token, ct) ? "kept" : "invalid";

        // Post/Redirect/Get, ohne Token in der neuen URL.
        return RedirectToPage("/Signup/Keep", new { culture = language.Code, status });
    }
}
