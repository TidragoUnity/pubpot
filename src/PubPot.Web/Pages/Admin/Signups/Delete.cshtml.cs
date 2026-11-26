using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubPot.Web.Admin;

namespace PubPot.Web.Pages.Admin.Signups;

/// <summary>
/// Endgültiges Löschen einer Anmeldung, z. B. bei einer DSGVO-Löschanfrage (E-028).
/// GET zeigt eine Rückfrage, erst der Button (POST mit Antiforgery-Token) löscht.
/// </summary>
public sealed class DeleteModel(AdminSignups signups) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    // Voller Name: "Signup" ist hier auch der Namespace der öffentlichen Seiten (PubPot.Web.Pages.Signup).
    public Data.Signup Signup { get; private set; } = null!;

    public IReadOnlyList<RelatedSignup> Related { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var signup = await signups.FindAsync(Id, ct);
        if (signup is null)
        {
            return NotFound();
        }

        Signup = signup;
        Related = await signups.RelatedAsync(signup, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var result = await signups.DeleteAsync(Id, ct);
        return result is null ? NotFound() : Redirect("/admin?deleted=True");
    }
}
