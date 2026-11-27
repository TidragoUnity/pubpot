using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PubPot.Web.Data;
using PubPot.Web.Forms;
using PubPot.Web.Localization;
using PubPot.Web.Monitoring;
using PubPot.Web.Signups;
using PubPot.Web.Spam;

namespace PubPot.Web.Pages.Signup;

[EnableRateLimiting(RateLimits.FormPosts)]
public sealed class CreatorModel(
    SignupService signups,
    FormTiming timing,
    IOptions<SpamOptions> spam,
    CurrentLanguage language,
    IStringLocalizer<SharedResource> text,
    PubPotMetrics metrics,
    ILogger<CreatorModel> logger) : SignupFormPageModel(timing, spam, language, text, metrics, logger)
{
    /// <summary>Reihenfolge der Plattformen im Formular.</summary>
    public static readonly CreatorPlatforms[] AllPlatforms =
        [CreatorPlatforms.Twitch, CreatorPlatforms.YouTube, CreatorPlatforms.TikTok, CreatorPlatforms.Kick];

    public static readonly AudienceSize[] AllAudienceSizes = Enum.GetValues<AudienceSize>();

    [BindProperty]
    public CreatorInput Input { get; set; } = new();

    public void OnGet() => IssueFormToken();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (RejectSpam() is { } spamResult)
        {
            return spamResult;
        }

        var platforms = Input.Platforms
            .Where(p => AllPlatforms.Contains(p))
            .Aggregate(CreatorPlatforms.None, (all, p) => all | p);
        if (platforms == CreatorPlatforms.None)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Platforms)}", Text["Validation.Platforms"]);
        }

        if (!string.IsNullOrWhiteSpace(Input.Channel) && !ChannelInput.IsValid(Input.Channel))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Channel)}", Text["Validation.Channel"]);
        }

        if (!ModelState.IsValid)
        {
            IssueFormToken();
            return Page();
        }

        await signups.SubmitAsync(new CreatorSubmission
        {
            Name = Input.Name!,
            Email = Input.Email!,
            Language = Language.Code,
            Message = Input.Message,
            Platforms = platforms,
            Channel = Input.Channel!,
            AudienceSize = Input.AudienceSize!.Value,
            Genres = Input.Genres!,
            ConfirmedAdult = Input.Adult,
        }, ct);

        return RedirectToThanks();
    }

    public bool IsChecked(CreatorPlatforms platform) => Input.Platforms.Contains(platform);

    public bool HasError(string field) =>
        ModelState.TryGetValue($"{nameof(Input)}.{field}", out var entry) && entry.Errors.Count > 0;
}

public sealed class CreatorInput
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

    [Display(Name = "Form.Platforms")]
    public List<CreatorPlatforms> Platforms { get; set; } = [];

    [Display(Name = "Form.Channel")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.MaxLength")]
    public string? Channel { get; set; }

    [Display(Name = "Form.Audience")]
    [Required(ErrorMessage = "Validation.Choose")]
    [EnumDataType(typeof(AudienceSize), ErrorMessage = "Validation.Invalid")]
    public AudienceSize? AudienceSize { get; set; }

    [Display(Name = "Form.Genres")]
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.MaxLength")]
    public string? Genres { get; set; }

    [Display(Name = "Form.Adult")]
    [MustBeTrue(ErrorMessage = "Validation.Adult")]
    public bool Adult { get; set; }

    [Display(Name = "Form.Message")]
    [StringLength(2000, ErrorMessage = "Validation.MaxLength")]
    public string? Message { get; set; }

    [Display(Name = "Consent.Text")]
    [MustBeTrue(ErrorMessage = "Validation.Consent")]
    public bool Consent { get; set; }
}
