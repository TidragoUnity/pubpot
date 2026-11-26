using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PubPot.Web.Admin;
using PubPot.Web.Spam;

namespace PubPot.Web.Pages.Admin;

/// <summary>
/// Login für das eine Admin-Konto. Schutz in Schichten (E-026): Sperre nach Fehlversuchen, Rate-Limit pro IP,
/// gleich lange Prüfung für Benutzername und Passwort, Antiforgery-Token.
/// </summary>
[EnableRateLimiting(RateLimits.AdminLogin)]
public sealed class LoginModel(
    IOptions<AdminOptions> options,
    AdminLoginThrottle throttle,
    TimeProvider time,
    ILogger<LoginModel> log) : PageModel
{
    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl()) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (throttle.RemainingLockout() is { } remaining)
        {
            Error = LockedMessage(remaining);
            return Page();
        }

        if (!AdminAuth.CheckCredentials(options.Value, Username, Password))
        {
            var lockout = throttle.RegisterFailure();
            // Nie den eingegebenen Namen loggen: Oft steht dort versehentlich das Passwort.
            if (lockout is null)
            {
                log.LogWarning("Admin-Login fehlgeschlagen ({Failures}. Fehlversuch).", throttle.Failures);
                Error = "Benutzername oder Passwort ist falsch.";
            }
            else
            {
                log.LogWarning("Admin-Login nach {Failures} Fehlversuchen für {Minutes} Minuten gesperrt.",
                    throttle.Failures, lockout.Value.TotalMinutes);
                Error = LockedMessage(lockout.Value);
            }

            Password = null;
            return Page();
        }

        throttle.Reset();
        await HttpContext.SignInAsync(
            AdminAuth.Scheme,
            AdminAuth.CreatePrincipal(options.Value, time.GetUtcNow()),
            new AuthenticationProperties { IsPersistent = false });
        log.LogInformation("Admin angemeldet.");
        return LocalRedirect(SafeReturnUrl());
    }

    /// <summary>Nur Ziele im Admin-Bereich. Verhindert, dass ein präparierter Link nach dem Login woandershin führt.</summary>
    private string SafeReturnUrl() =>
        ReturnUrl is { } url && Url.IsLocalUrl(url) && AdminAuth.IsAdminPath(new PathString(url.Split('?')[0]))
            ? url
            : AdminAuth.PathPrefix;

    private static string LockedMessage(TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return $"Zu viele Fehlversuche. Der Login ist für {(minutes == 1 ? "1 Minute" : $"{minutes} Minuten")} gesperrt.";
    }
}
