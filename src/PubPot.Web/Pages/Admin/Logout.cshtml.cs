using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubPot.Web.Admin;

namespace PubPot.Web.Pages.Admin;

/// <summary>
/// Abmelden nur per POST (mit Antiforgery-Token). Ein GET-Link ließe sich von fremden Seiten auslösen.
/// Die Sitzung wird zusätzlich gesperrt, damit ein kopiertes Cookie danach nicht mehr gilt.
/// </summary>
public sealed class LogoutModel(AdminSessions sessions, ILogger<LogoutModel> log) : PageModel
{
    public IActionResult OnGet() => LocalRedirect(AdminAuth.PathPrefix);

    public async Task<IActionResult> OnPostAsync()
    {
        sessions.Revoke(User);
        await HttpContext.SignOutAsync(AdminAuth.Scheme);
        log.LogInformation("Admin abgemeldet.");
        return LocalRedirect(AdminAuth.PathPrefix + "/login");
    }
}
