namespace PubPot.Web.Admin;

/// <summary>
/// Das eine Admin-Konto (Abschnitt "Admin", in Docker also <c>Admin__Username</c> und <c>Admin__PasswordHash</c>).
/// Im Repo und in der Konfiguration steht nie das Passwort selbst, nur sein Hash (siehe E-025).
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Username { get; set; } = "admin";

    /// <summary>
    /// Erzeugt mit <c>dotnet run --project src/PubPot.Web -- hash-password</c>.
    /// Leer bedeutet: Der Admin-Bereich ist aus (jede Admin-Seite antwortet mit 404).
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>Nach so vielen falschen Logins in Folge wird der Login gesperrt.</summary>
    public int MaxFailedLogins { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Ohne Aktivität endet die Sitzung nach dieser Zeit (jede Anfrage verlängert sie).</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Spätestens nach dieser Zeit muss man sich neu anmelden, auch bei Aktivität.</summary>
    public TimeSpan MaxSessionLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Rate-Limit für Login-Versuche pro IP-Adresse (zusätzlich zur Sperre).</summary>
    public int LoginPostsPerWindow { get; set; } = 10;

    public TimeSpan LoginWindow { get; set; } = TimeSpan.FromMinutes(10);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(PasswordHash);

    /// <summary>Prüft beim Start, ob die Einstellungen brauchbar sind (fail fast statt Fehler beim ersten Login).</summary>
    public static bool IsValid(AdminOptions options) =>
        !string.IsNullOrWhiteSpace(options.Username)
        && options.Username.Length <= 64
        && (!options.IsEnabled || AdminPassword.IsWellFormedHash(options.PasswordHash))
        && options.MaxFailedLogins >= 1
        && options.LockoutDuration > TimeSpan.Zero
        && options.IdleTimeout > TimeSpan.Zero
        && options.MaxSessionLifetime >= options.IdleTimeout
        && options.LoginPostsPerWindow >= 1
        && options.LoginWindow > TimeSpan.Zero;
}
