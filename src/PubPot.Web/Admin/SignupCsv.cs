using System.Globalization;
using System.Text;
using PubPot.Web.Data;

namespace PubPot.Web.Admin;

/// <summary>
/// CSV-Export der Anmeldungen, so gebaut, dass Excel ihn direkt richtig öffnet (E-029):
/// UTF-8 mit BOM (sonst sind Umlaute kaputt), Semikolon als Trenner (deutsches Excel), Zeilenende CRLF.
/// Zellen, die mit <c>= + - @</c> beginnen, bekommen ein <c>'</c> vorangestellt: Sonst führt Excel sie als
/// Formel aus („CSV-Injection“), z. B. einen Namen wie <c>=HYPERLINK("https://boese.example")</c>.
/// </summary>
public static class SignupCsv
{
    public const char Separator = ';';

    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly string[] Header =
    [
        "ID", "Art", "Name", "E-Mail", "Sprache", "Eingegangen", "Letzte Einreichung", "Bestätigt",
        "Status", "Nächster Schritt", "Notiz", "Admin geändert",
        "Studio", "Spiel", "Steam-App-ID", "Release", "Demo", "Next-Fest-Pilot", "Publisher",
        "Plattformen", "Kanal", "Zuschauer", "Genres", "18+",
        "Nachricht", "Einwilligung Version", "Einwilligung am",
    ];

    public static byte[] Write(IEnumerable<Signup> signups)
    {
        var text = new StringBuilder();
        AppendRow(text, Header);
        foreach (var s in signups)
        {
            AppendRow(text, Row(s));
        }

        // Preamble = BOM (EF BB BF). Daran erkennt Excel, dass die Datei UTF-8 ist.
        return [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    public static string FileName(DateTimeOffset now) =>
        $"pubpot-anmeldungen-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";

    private static string?[] Row(Signup s)
    {
        var dev = s as DeveloperSignup;
        var creator = s as CreatorSignup;
        return
        [
            s.Id.ToString(),
            AdminText.Label(s.Kind),
            s.Name,
            s.Email,
            s.Language,
            AdminText.DateTime(s.CreatedAt),
            AdminText.DateTime(s.UpdatedAt),
            s.ConfirmedAt is { } confirmed ? AdminText.DateTime(confirmed) : "",
            AdminText.Label(s.PipelineStatus),
            s.NextStepOn is { } next ? AdminText.Date(next) : "",
            s.AdminNote,
            s.AdminUpdatedAt is { } adminUpdated ? AdminText.DateTime(adminUpdated) : "",
            dev?.Studio,
            dev?.GameTitle,
            dev?.SteamAppId.ToString(CultureInfo.InvariantCulture),
            dev is null ? null : dev.AlreadyReleased ? "veröffentlicht" : dev.PlannedRelease is { } month ? AdminText.Month(month) : "",
            dev is null ? null : AdminText.YesNo(dev.HasDemo),
            dev is null ? null : AdminText.YesNo(dev.InterestedInNextFestPilot),
            dev is null ? null : AdminText.YesNo(dev.HasPublisher),
            creator is null ? null : AdminText.Label(creator.Platforms),
            creator?.Channel,
            creator is null ? null : AdminText.Label(creator.AudienceSize),
            creator?.Genres,
            creator is null ? null : AdminText.YesNo(creator.ConfirmedAdult),
            s.Message,
            s.ConsentVersion,
            AdminText.DateTime(s.ConsentGivenAt),
        ];
    }

    private static void AppendRow(StringBuilder text, IEnumerable<string?> cells)
    {
        text.AppendJoin(Separator, cells.Select(Cell));
        text.Append("\r\n");
    }

    /// <summary>Eine Zelle: entschärft (Formeln) und bei Bedarf in Anführungszeichen (RFC 4180).</summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
