using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace PubPot.Web.Forms;

/// <summary>
/// Strengere E-Mail-Prüfung als [EmailAddress] (die nur auf ein "@" achtet):
/// gültige Adresse laut <see cref="MailAddress"/>, keine Leerzeichen, kein Anzeigename, Domain mit Punkt.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EmailAttribute : ValidationAttribute
{
    public EmailAttribute() => ErrorMessage = "Validation.Email";

    public override bool IsValid(object? value)
    {
        if (value is not string text || text.Length == 0)
        {
            return true; // leer prüft [Required]
        }

        var email = text.Trim();
        return !email.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(email, out var address)
            && address.Address == email
            && address.Host.Contains('.', StringComparison.Ordinal);
    }
}

/// <summary>Häkchen muss gesetzt sein (Einwilligung, Altersbestätigung).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}
