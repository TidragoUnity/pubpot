using Microsoft.AspNetCore.Identity;

namespace PubPot.Web.Admin;

/// <summary>
/// Hasht und prüft das Admin-Passwort mit dem <see cref="PasswordHasher{TUser}"/> aus ASP.NET Core Identity:
/// PBKDF2 mit HMAC-SHA512, 100 000 Runden und zufälligem Salt. Das Verfahren ist absichtlich langsam,
/// damit ein gestohlener Hash nicht schnell durchprobiert werden kann (E-025).
/// </summary>
public static class AdminPassword
{
    public const int MinLength = 12;

    // Kennung des Hash-Formats "Identity V3" (erstes Byte des Hashes).
    private const byte FormatV3 = 0x01;

    // Format, Algorithmus, Runden, Salt-Länge: je 4 Bytes nach dem Kennungsbyte, danach Salt und Schlüssel.
    private const int MinV3Length = 1 + 4 + 4 + 4 + 16 + 32;

    private static readonly PasswordHasher<AdminAccount> Hasher = new();

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Hasher.HashPassword(AdminAccount.Instance, password);
    }

    /// <summary>
    /// <c>true</c>, wenn das Passwort zum Hash passt. Ein kaputter Hash ergibt <c>false</c> statt einer Ausnahme.
    /// </summary>
    public static bool Verify(string hash, string password)
    {
        if (!IsWellFormedHash(hash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        // "SuccessRehashNeeded" heißt: richtig, aber mit älteren Parametern erzeugt. Das zählt auch als richtig.
        return Hasher.VerifyHashedPassword(AdminAccount.Instance, hash, password) != PasswordVerificationResult.Failed;
    }

    /// <summary>Base64 und Format "Identity V3". Erkennt Tippfehler beim Kopieren in die Umgebungsvariable.</summary>
    public static bool IsWellFormedHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var buffer = new byte[hash.Length];
        return Convert.TryFromBase64String(hash, buffer, out var length)
            && length >= MinV3Length
            && buffer[0] == FormatV3;
    }

    /// <summary>Der PasswordHasher verlangt einen Benutzertyp. Es gibt nur ein Konto, also reicht ein leerer.</summary>
    private sealed class AdminAccount
    {
        public static readonly AdminAccount Instance = new();
    }
}
