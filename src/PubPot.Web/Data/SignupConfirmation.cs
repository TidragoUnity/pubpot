namespace PubPot.Web.Data;

/// <summary>
/// Ein offener Bestätigungslink. Gespeichert ist nur der SHA-256-Hash des Tokens:
/// Wer die Datenbank liest, kann damit keine Anmeldung bestätigen.
/// </summary>
public sealed class SignupConfirmation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SignupId { get; set; }

    public Signup Signup { get; set; } = null!;

    /// <summary>SHA-256 des Tokens aus dem Link (32 Byte).</summary>
    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Nur bei bereits bestätigten Anmeldungen: die neuen Angaben als JSON. Sie werden erst übernommen,
    /// wenn der neue Link bestätigt wurde – sonst könnte jeder mit fremder E-Mail fremde Daten überschreiben.
    /// </summary>
    public string? PendingChanges { get; set; }
}
