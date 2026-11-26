using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PubPot.Web.Spam;

public enum FormTimingResult
{
    Ok,
    TooFast,
    Invalid,
}

/// <summary>
/// Mindestausfüllzeit: Beim Anzeigen bekommt das Formular einen signierten und verschlüsselten Zeitstempel
/// (ASP.NET Data Protection). Ein Bot kann ihn weder lesen noch fälschen – er kann nur echte Wartezeit "kaufen".
/// </summary>
public sealed class FormTiming(IDataProtectionProvider dataProtection, TimeProvider time)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("PubPot.Forms.Timing.v1");

    public string Issue() =>
        _protector.Protect(time.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

    public FormTimingResult Check(string? token, int minSeconds)
    {
        if (string.IsNullOrEmpty(token))
        {
            return FormTimingResult.Invalid;
        }

        try
        {
            var issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(
                long.Parse(_protector.Unprotect(token), CultureInfo.InvariantCulture));
            return time.GetUtcNow() - issuedAt < TimeSpan.FromSeconds(minSeconds)
                ? FormTimingResult.TooFast
                : FormTimingResult.Ok;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or OverflowException)
        {
            return FormTimingResult.Invalid; // manipuliert, abgeschnitten oder mit altem Schlüssel
        }
    }
}
