using PubPot.Web.Data;

namespace PubPot.Web.Signups;

/// <summary>
/// Eine geprüfte Einreichung aus einem Anmeldeformular – unabhängig von HTML und Datenbank.
/// Sie weiß, wie aus ihr eine neue Anmeldung wird und wie sie eine bestehende aktualisiert (Zusammenführen).
/// </summary>
public abstract record SignupSubmission
{
    public required string Name { get; init; }

    public required string Email { get; init; }

    /// <summary>Sprache der Seite, auf der das Formular ausgefüllt wurde ("de"/"en").</summary>
    public required string Language { get; init; }

    public string? Message { get; init; }

    public abstract SignupKind Kind { get; }

    /// <summary>Neue, noch leere Anmeldung der passenden Art.</summary>
    public abstract Signup CreateEntity();

    /// <summary>Überträgt alle Angaben auf eine (neue oder bestehende) Anmeldung.</summary>
    public virtual void ApplyTo(Signup signup)
    {
        signup.Name = Name.Trim();
        signup.Email = Email.Trim();
        signup.NormalizedEmail = Signup.NormalizeEmail(Email);
        signup.Language = Language;
        signup.Message = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim();
    }
}

public sealed record DeveloperSubmission : SignupSubmission
{
    public required string Studio { get; init; }

    public required string GameTitle { get; init; }

    public long SteamAppId { get; init; }

    public bool AlreadyReleased { get; init; }

    public DateOnly? PlannedRelease { get; init; }

    public bool HasDemo { get; init; }

    public bool InterestedInNextFestPilot { get; init; }

    public bool HasPublisher { get; init; }

    public override SignupKind Kind => SignupKind.Developer;

    public override Signup CreateEntity() => new DeveloperSignup
    {
        Email = "",
        NormalizedEmail = "",
        Name = "",
        Language = "",
        ConsentVersion = "",
        Studio = "",
        GameTitle = "",
    };

    public override void ApplyTo(Signup signup)
    {
        base.ApplyTo(signup);
        var developer = (DeveloperSignup)signup;
        developer.Studio = Studio.Trim();
        developer.GameTitle = GameTitle.Trim();
        developer.SteamAppId = SteamAppId;
        developer.AlreadyReleased = AlreadyReleased;
        developer.PlannedRelease = AlreadyReleased ? null : PlannedRelease;
        developer.HasDemo = HasDemo;
        developer.InterestedInNextFestPilot = InterestedInNextFestPilot;
        developer.HasPublisher = HasPublisher;
    }
}

public sealed record CreatorSubmission : SignupSubmission
{
    public CreatorPlatforms Platforms { get; init; }

    public required string Channel { get; init; }

    public AudienceSize AudienceSize { get; init; }

    public required string Genres { get; init; }

    public bool ConfirmedAdult { get; init; }

    public override SignupKind Kind => SignupKind.Creator;

    public override Signup CreateEntity() => new CreatorSignup
    {
        Email = "",
        NormalizedEmail = "",
        Name = "",
        Language = "",
        ConsentVersion = "",
        Channel = "",
        Genres = "",
    };

    public override void ApplyTo(Signup signup)
    {
        base.ApplyTo(signup);
        var creator = (CreatorSignup)signup;
        creator.Platforms = Platforms;
        creator.Channel = Channel.Trim();
        creator.AudienceSize = AudienceSize;
        creator.Genres = Genres.Trim();
        creator.ConfirmedAdult = ConfirmedAdult;
    }
}
