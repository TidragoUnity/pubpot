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

    public bool IsEnabled => !string.IsNullOrWhiteSpace(PasswordHash);

    /// <summary>Prüft beim Start, ob die Einstellungen brauchbar sind (fail fast statt Fehler beim ersten Login).</summary>
    public static bool IsValid(AdminOptions options) =>
        !string.IsNullOrWhiteSpace(options.Username)
        && options.Username.Length <= 64
        && (!options.IsEnabled || AdminPassword.IsWellFormedHash(options.PasswordHash));
}
