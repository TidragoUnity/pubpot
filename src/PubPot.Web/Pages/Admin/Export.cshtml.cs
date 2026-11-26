using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Admin;
using PubPot.Web.Data;

namespace PubPot.Web.Pages.Admin;

/// <summary>
/// CSV-Export der gerade gefilterten Liste (E-029). Nur per POST aus dem Filterformular der Liste:
/// So kommen Filter und Suchtext mit, ohne dass der Suchtext in der URL steht.
/// </summary>
public sealed class ExportModel(AdminSignups signups, TimeProvider time, ILogger<ExportModel> log) : PageModel
{
    [BindProperty]
    public SignupKind? Kind { get; set; }

    [BindProperty]
    public bool? Confirmed { get; set; }

    [BindProperty]
    public PipelineStatus? Status { get; set; }

    [BindProperty]
    public string? Language { get; set; }

    [BindProperty]
    public string? Search { get; set; }

    public IActionResult OnGet() => LocalRedirect(AdminAuth.PathPrefix);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var search = Search?.Trim();
        if (search is { Length: > SignupFilter.SearchMaxLength })
        {
            search = search[..SignupFilter.SearchMaxLength];
        }

        var language = Language is { Length: > 0 } && Localization.SupportedCultures.IsSupported(Language) ? Language : null;
        var rows = await signups.Query(new SignupFilter(Kind, Confirmed, Status, language, search)).ToListAsync(ct);

        log.LogInformation("CSV-Export mit {Count} Anmeldungen erstellt.", rows.Count);
        return File(SignupCsv.Write(rows), SignupCsv.ContentType, SignupCsv.FileName(time.GetUtcNow()));
    }
}
