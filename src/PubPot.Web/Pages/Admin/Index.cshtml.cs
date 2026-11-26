using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubPot.Web.Admin;
using PubPot.Web.Data;

namespace PubPot.Web.Pages.Admin;

/// <summary>
/// Liste aller Anmeldungen mit Filter und Suche.
/// Filter ohne Personenbezug (Art, bestätigt, Status, Sprache, Seite) stehen in der URL: Neu laden und Lesezeichen
/// funktionieren. Der Suchtext enthält oft E-Mail-Adressen oder Namen und geht deshalb nur per POST, damit er nicht
/// in Server-Logs, im Browserverlauf oder im Referrer landet ("keine personenbezogenen Daten in URLs").
/// </summary>
public sealed class IndexModel(AdminSignups signups) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public SignupKind? Kind { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? Confirmed { get; set; }

    [BindProperty(SupportsGet = true)]
    public PipelineStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Language { get; set; }

    /// <summary>Seitennummer als "p": "page" ist in Razor Pages reserviert (enthält den Seitenpfad).</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Nur per POST, nie aus der URL.</summary>
    [BindProperty]
    public string? Search { get; set; }

    public SignupPage Result { get; private set; } = new([], 0, 1, 1);

    public bool IsFiltered => Kind is not null || Confirmed is not null || Status is not null
        || !string.IsNullOrEmpty(Language) || !string.IsNullOrWhiteSpace(Search);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Search = null;
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Search = Search?.Trim();
        if (Search is { Length: > SignupFilter.SearchMaxLength })
        {
            Search = Search[..SignupFilter.SearchMaxLength];
        }

        // Ohne Suchtext: zurück zur GET-Adresse mit den Filtern (Post/Redirect/Get), dann klappt auch Neu laden.
        if (string.IsNullOrEmpty(Search))
        {
            return RedirectToPage(new
            {
                kind = Kind,
                confirmed = Confirmed,
                status = Status,
                language = string.IsNullOrEmpty(Language) ? null : Language,
                p = PageNumber > 1 ? PageNumber : (int?)null,
            });
        }

        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(Language) && !Localization.SupportedCultures.IsSupported(Language))
        {
            Language = null;
        }

        var filter = new SignupFilter(Kind, Confirmed, Status, Language, Search);
        Result = await signups.ListAsync(filter, PageNumber, ct);
        PageNumber = Result.PageNumber;
    }
}
