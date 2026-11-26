using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PubPot.Web.Forms;
using PubPot.Web.Localization;
using PubPot.Web.Signups;
using PubPot.Web.Spam;

namespace PubPot.Web.Pages.Signup;

[EnableRateLimiting(RateLimits.FormPosts)]
public sealed class DeveloperModel(
    SignupService signups,
    TimeProvider time,
    FormTiming timing,
    IOptions<SpamOptions> spam,
    CurrentLanguage language,
    IStringLocalizer<SharedResource> text,
    ILogger<DeveloperModel> logger) : SignupFormPageModel(timing, spam, language, text, logger)
{
    [BindProperty]
    public DeveloperInput Input { get; set; } = new();

    public IReadOnlyList<(string Value, string Label)> ReleaseMonths { get; private set; } = [];

    public void OnGet()
    {
        ReleaseMonths = ReleaseChoice.Months(time.GetUtcNow());
        IssueFormToken();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        ReleaseMonths = ReleaseChoice.Months(now);

        if (RejectSpam() is { } spamResult)
        {
            return spamResult;
        }

        // Prüfungen, die Attribute nicht abdecken. Leere Felder meldet bereits [Required].
        long steamAppId = 0;
        if (!string.IsNullOrWhiteSpace(Input.SteamLink) && !SteamLink.TryParse(Input.SteamLink, out steamAppId))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SteamLink)}", Text["Validation.SteamLink"]);
        }

        var released = false;
        DateOnly? plannedRelease = null;
        if (!string.IsNullOrEmpty(Input.Release) && !ReleaseChoice.TryParse(Input.Release, now, out released, out plannedRelease))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Release)}", Text["Validation.Release"]);
        }

        if (!ModelState.IsValid)
        {
            IssueFormToken();
            return Page();
        }

        await signups.SubmitAsync(new DeveloperSubmission
        {
            Name = Input.Name!,
            Email = Input.Email!,
            Language = Language.Code,
            Message = Input.Message,
            Studio = Input.Studio!,
            GameTitle = Input.GameTitle!,
            SteamAppId = steamAppId,
            AlreadyReleased = released,
            PlannedRelease = plannedRelease,
            HasDemo = Input.HasDemo!.Value,
            InterestedInNextFestPilot = Input.NextFestPilot!.Value,
            HasPublisher = Input.HasPublisher!.Value,
        }, ct);

        // Post/Redirect/Get: Neuladen der Danke-Seite schickt das Formular nicht erneut ab.
        return RedirectToThanks();
    }
}

/// <summary>Formularfelder. Fehlermeldungen und Beschriftungen sind Schlüssel in SharedResource.resx.</summary>
public sealed class DeveloperInput
{
    [Display(Name = "Form.Name")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.MaxLength")]
    public string? Name { get; set; }

    [Display(Name = "Form.Email")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(254, ErrorMessage = "Validation.MaxLength")]
    [Email]
    public string? Email { get; set; }

    [Display(Name = "Form.Studio")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.MaxLength")]
    public string? Studio { get; set; }

    [Display(Name = "Form.GameTitle")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(150, ErrorMessage = "Validation.MaxLength")]
    public string? GameTitle { get; set; }

    [Display(Name = "Form.SteamLink")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(300, ErrorMessage = "Validation.MaxLength")]
    public string? SteamLink { get; set; }

    [Display(Name = "Form.Release")]
    [Required(ErrorMessage = "Validation.Choose")]
    public string? Release { get; set; }

    [Display(Name = "Form.HasDemo")]
    [Required(ErrorMessage = "Validation.Choose")]
    public bool? HasDemo { get; set; }

    [Display(Name = "Form.NextFestPilot")]
    [Required(ErrorMessage = "Validation.Choose")]
    public bool? NextFestPilot { get; set; }

    [Display(Name = "Form.HasPublisher")]
    [Required(ErrorMessage = "Validation.Choose")]
    public bool? HasPublisher { get; set; }

    [Display(Name = "Form.Message")]
    [StringLength(2000, ErrorMessage = "Validation.MaxLength")]
    public string? Message { get; set; }

    [Display(Name = "Consent.Text")]
    [MustBeTrue(ErrorMessage = "Validation.Consent")]
    public bool Consent { get; set; }
}
