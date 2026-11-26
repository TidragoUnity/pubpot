using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace PubPot.Web.Signups;

/// <summary>
/// Token für den Bestätigungslink: 32 Zufallsbytes (256 Bit), Base64Url-kodiert für die URL.
/// In der Datenbank steht nur der SHA-256-Hash – wie bei Passwörtern, nur dass ein schneller Hash hier reicht,
/// weil das Token selbst zufällig und lang ist (nicht erratbar, kein Wörterbuchangriff möglich).
/// </summary>
public static class ConfirmationToken
{
    /// <summary>So lange gilt ein Link – passend zur automatischen Löschung unbestätigter Anmeldungen.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static (string Token, byte[] Hash) Create()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    /// <summary>Grobe Vorprüfung, bevor die Datenbank gefragt wird (43 Zeichen bei 32 Bytes).</summary>
    public static bool LooksValid(string? token) => token is { Length: 43 } && Base64Url.IsValid(token);
}
