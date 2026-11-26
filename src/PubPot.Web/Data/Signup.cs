namespace PubPot.Web.Data;

/// <summary>Entwickler oder Creator. Wird als Text gespeichert ("Developer"/"Creator"), gut lesbar in der Datenbank.</summary>
public enum SignupKind
{
    Developer = 1,
    Creator = 2,
}

/// <summary>
/// Eine Anmeldung. Pro E-Mail-Adresse und Rolle gibt es genau eine (siehe docs/ENTSCHEIDUNGEN.md, E-020).
/// Entwickler und Creator liegen in derselben Tabelle ("Table per Hierarchy"); die Spalte <c>kind</c> unterscheidet sie.
/// </summary>
public abstract class Signup
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public SignupKind Kind { get; protected set; }

    /// <summary>Wie eingegeben (nur Leerzeichen entfernt) – für die Anzeige und den Versand.</summary>
    public required string Email { get; set; }

    /// <summary>Kleingeschrieben – für den Vergleich "gleiche E-Mail?" (eindeutig zusammen mit <see cref="Kind"/>).</summary>
    public required string NormalizedEmail { get; set; }

    public required string Name { get; set; }

    /// <summary>Sprache der Anmeldung ("de"/"en") – darin schreiben wir zurück.</summary>
    public required string Language { get; set; }

    public string? Message { get; set; }

    /// <summary>Version des Einwilligungstextes, dem zugestimmt wurde, z. B. "2026-11-26".</summary>
    public required string ConsentVersion { get; set; }

    public DateTimeOffset ConsentGivenAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Erst gesetzt, wenn der Link in der Bestätigungsmail angeklickt wurde (Double-Opt-in).</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    public bool IsConfirmed => ConfirmedAt is not null;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

public sealed class DeveloperSignup : Signup
{
    public DeveloperSignup() => Kind = SignupKind.Developer;

    public required string Studio { get; set; }

    public required string GameTitle { get; set; }

    /// <summary>Die App-ID aus dem Steam-Link (store.steampowered.com/app/&lt;ID&gt;).</summary>
    public long SteamAppId { get; set; }

    /// <summary>Bereits veröffentlicht? Dann ist <see cref="PlannedRelease"/> leer.</summary>
    public bool AlreadyReleased { get; set; }

    /// <summary>Geplanter Release-Monat (immer der 1. des Monats).</summary>
    public DateOnly? PlannedRelease { get; set; }

    public bool HasDemo { get; set; }

    public bool InterestedInNextFestPilot { get; set; }

    public bool HasPublisher { get; set; }

    public string SteamUrl => $"https://store.steampowered.com/app/{SteamAppId}/";
}

/// <summary>Plattformen eines Creators. Mehrfachauswahl, gespeichert als Bitmaske (Zahl).</summary>
[Flags]
public enum CreatorPlatforms
{
    None = 0,
    Twitch = 1,
    YouTube = 2,
    TikTok = 4,
    Kick = 8,
}

/// <summary>Ungefähre Ø-Zuschauer bzw. Views pro Video.</summary>
public enum AudienceSize
{
    Under10 = 1,
    From10To24 = 2,
    From25To49 = 3,
    From50To99 = 4,
    From100 = 5,
}

public sealed class CreatorSignup : Signup
{
    public CreatorSignup() => Kind = SignupKind.Creator;

    public CreatorPlatforms Platforms { get; set; }

    /// <summary>Kanal-Link oder Twitch-Login.</summary>
    public required string Channel { get; set; }

    public AudienceSize AudienceSize { get; set; }

    public required string Genres { get; set; }

    /// <summary>"Ich bin mindestens 18 Jahre alt" wurde bestätigt.</summary>
    public bool ConfirmedAdult { get; set; }
}
