using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubPot.Web.Admin;
using PubPot.Web.Data;

namespace PubPot.Web.Pages.Admin.Signups;

/// <summary>
/// Eine Anmeldung mit allen Angaben. Hier werden Pipeline-Status, Notiz und nächster Schritt gepflegt.
/// In der URL steht nur die ID, nie Name oder E-Mail.
/// </summary>
public sealed class DetailModel(AdminSignups signups) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    /// <summary>Nach dem Speichern (Post/Redirect/Get) für den Hinweis „Gespeichert“.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Saved { get; set; }

    [BindProperty]
    public PipelineStatus? Status { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    [BindProperty]
    public DateOnly? NextStepOn { get; set; }

    // Voller Name: "Signup" ist hier auch der Namespace der öffentlichen Seiten (PubPot.Web.Pages.Signup).
    public Data.Signup Signup { get; private set; } = null!;

    public bool HasPendingChanges { get; private set; }

    public IReadOnlyList<string> Errors { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        Status = Signup.PipelineStatus;
        Note = Signup.AdminNote;
        NextStepOn = Signup.NextStepOn;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        Errors = Validate();
        if (Errors.Count > 0)
        {
            return Page();
        }

        if (!await signups.UpdatePipelineAsync(Id, new PipelineUpdate(Status!.Value, Note, NextStepOn), ct))
        {
            return NotFound(); // inzwischen gelöscht
        }

        return RedirectToPage(new { id = Id, saved = true });
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        var signup = await signups.FindAsync(Id, ct);
        if (signup is null)
        {
            return false;
        }

        Signup = signup;
        HasPendingChanges = await signups.HasPendingChangesAsync(Id, ct);
        return true;
    }

    private List<string> Validate()
    {
        // Fehler der Umwandlung (z. B. ungültiges Datum) kommen schon aus dem Model Binding.
        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();

        if (Status is not { } status || !Enum.IsDefined(status))
        {
            errors.Add("Bitte einen Status wählen.");
        }

        if (Note is { Length: > AppDbContext.AdminNoteMaxLength })
        {
            errors.Add($"Die Notiz darf höchstens {AppDbContext.AdminNoteMaxLength} Zeichen lang sein.");
        }

        if (NextStepOn is { Year: < 2000 or > 2100 })
        {
            errors.Add("Das Datum des nächsten Schritts ist ungültig.");
        }

        return errors.Distinct().ToList();
    }
}
