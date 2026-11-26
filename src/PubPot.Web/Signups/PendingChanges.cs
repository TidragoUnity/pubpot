using System.Text.Json;
using PubPot.Web.Data;

namespace PubPot.Web.Signups;

/// <summary>
/// Neue Angaben zu einer bereits bestätigten Anmeldung, gespeichert als JSON am Bestätigungslink.
/// Sie werden erst übernommen, wenn der Link bestätigt wird (siehe docs/ENTSCHEIDUNGEN.md, E-020).
/// </summary>
public static class PendingChanges
{
    private sealed record Envelope(DateTimeOffset SubmittedAt, string ConsentVersion, JsonElement Submission);

    public static string Serialize(SignupSubmission submission, DateTimeOffset submittedAt) =>
        JsonSerializer.Serialize(new
        {
            SubmittedAt = submittedAt,
            ConsentVersion = ConsentText.Version,
            // Als object übergeben → es wird der tatsächliche Typ (Developer/Creator) mit allen Feldern gespeichert.
            Submission = (object)submission,
        });

    public static void Apply(Signup signup, string json)
    {
        var envelope = JsonSerializer.Deserialize<Envelope>(json)
            ?? throw new InvalidOperationException("Leere ausstehende Änderung.");

        SignupSubmission submission = signup.Kind switch
        {
            SignupKind.Developer => envelope.Submission.Deserialize<DeveloperSubmission>()!,
            SignupKind.Creator => envelope.Submission.Deserialize<CreatorSubmission>()!,
            _ => throw new InvalidOperationException($"Unbekannte Art {signup.Kind}."),
        };

        submission.ApplyTo(signup);
        signup.ConsentVersion = envelope.ConsentVersion;
        signup.ConsentGivenAt = envelope.SubmittedAt;
    }
}
