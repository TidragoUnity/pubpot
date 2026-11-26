using System.ComponentModel.DataAnnotations;

namespace PubPot.Web.Spam;

/// <summary>Spam-Schutz ohne Drittanbieter (Abschnitt "Spam").</summary>
public sealed class SpamOptions
{
    /// <summary>Wer ein Formular schneller absendet, ist kein Mensch. In Tests 0.</summary>
    [Range(0, 60)]
    public int MinFillSeconds { get; set; } = 3;

    /// <summary>Höchstens so viele Formular-Absendungen pro IP-Adresse und Zeitfenster.</summary>
    [Range(1, 100_000)]
    public int MaxPostsPerWindow { get; set; } = 5;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Höchstens eine Bestätigungsmail pro Anmeldung in diesem Zeitraum – sonst könnte jemand
    /// eine fremde Adresse mit Mails fluten, indem er das Formular immer wieder absendet.
    /// </summary>
    public TimeSpan MailCooldown { get; set; } = TimeSpan.FromMinutes(5);
}
