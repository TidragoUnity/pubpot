using System.Globalization;
using PubPot.Web.Data;

namespace PubPot.Web.Admin;

/// <summary>Deutsche Bezeichnungen und Formate für den Admin-Bereich (nur Deutsch, E-024).</summary>
public static class AdminText
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Zeitzone für die Anzeige. Gespeichert wird immer UTC.</summary>
    private static readonly TimeZoneInfo Berlin = FindBerlin();

    public static IReadOnlyList<PipelineStatus> Statuses { get; } = Enum.GetValues<PipelineStatus>();

    public static string Label(PipelineStatus status) => status switch
    {
        PipelineStatus.New => "neu",
        PipelineStatus.Contacted => "kontaktiert",
        PipelineStatus.Conversation => "Gespräch",
        PipelineStatus.Pilot => "Pilot",
        PipelineStatus.Declined => "abgesagt",
        _ => status.ToString(),
    };

    public static string Label(SignupKind kind) => kind switch
    {
        SignupKind.Developer => "Entwickler",
        SignupKind.Creator => "Creator",
        _ => kind.ToString(),
    };

    public static string LanguageLabel(string language) => language switch
    {
        "de" => "Deutsch",
        "en" => "Englisch",
        _ => language,
    };

    public static string Date(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Berlin).ToString("dd.MM.yyyy", German);

    public static string DateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Berlin).ToString("dd.MM.yyyy HH:mm", German);

    public static string Date(DateOnly value) => value.ToString("dd.MM.yyyy", German);

    private static TimeZoneInfo FindBerlin()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            // Container ohne Zeitzonen-Daten: lieber UTC anzeigen als abstürzen.
            return TimeZoneInfo.Utc;
        }
    }
}
