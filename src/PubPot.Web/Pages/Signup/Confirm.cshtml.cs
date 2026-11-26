using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PubPot.Web.Localization;
using PubPot.Web.Signups;
using PubPot.Web.Spam;

namespace PubPot.Web.Pages.Signup;

/// <summary>
/// Bestätigungsseite für den Link aus der Mail.
/// GET prüft nur und zeigt einen Button; erst der Klick (POST) bestätigt. Grund: Mail-Programme und Virenscanner
/// öffnen Links oft automatisch vorab – ein reiner GET würde Anmeldungen ohne menschlichen Klick bestätigen.
/// </summary>
[EnableRateLimiting(RateLimits.FormPosts)]
public sealed class ConfirmModel(SignupService signups, CurrentLanguage language) : PageModel
{
    public enum View
    {
        Prompt,
        Confirmed,
        Invalid,
        Expired,
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
            "confirmed" => View.Confirmed,
            "expired" => View.Expired,
            "invalid" => View.Invalid,
            _ => await signups.CheckAsync(Token, ct) switch
            {
                ConfirmationStatus.Valid => View.Prompt,
                ConfirmationStatus.Expired => View.Expired,
                _ => View.Invalid,
            },
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var status = await signups.ConfirmAsync(Token, ct) switch
        {
            ConfirmationStatus.Valid => "confirmed",
            ConfirmationStatus.Expired => "expired",
            _ => "invalid",
        };

        // Post/Redirect/Get – ohne Token in der neuen URL.
        return RedirectToPage("/Signup/Confirm", new { culture = language.Code, status });
    }
}
