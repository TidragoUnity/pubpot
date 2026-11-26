using System.ComponentModel.DataAnnotations;

namespace PubPot.Web.Email;

/// <summary>
/// SMTP-Zugang (Abschnitt "Smtp", per Umgebungsvariable z. B. <c>Smtp__Host</c>, <c>Smtp__Password</c>).
/// Lokal: Mailpit auf localhost:1025 ohne Verschlüsselung (appsettings.Development.json).
/// </summary>
public sealed class SmtpOptions
{
    [Required]
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>None (nur lokal/Mailpit), StartTls (Port 587, üblich) oder SslOnConnect (Port 465).</summary>
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "";

    public string FromName { get; set; } = "PubPot";
}

public enum SmtpSecurity
{
    None,
    StartTls,
    SslOnConnect,
}

/// <summary>Mail-Ausgang (Abschnitt "Outbox").</summary>
public sealed class OutboxOptions
{
    /// <summary>Hintergrundversand an/aus. In Tests aus – dort wird der Versand direkt angestoßen.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Wie oft nach fälligen Mails gesucht wird.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Nach so vielen Fehlversuchen wird eine Mail aufgegeben (bleibt zur Fehlersuche in der Tabelle).</summary>
    [Range(1, 50)]
    public int MaxAttempts { get; set; } = 10;
}
