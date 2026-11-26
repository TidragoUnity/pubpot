using System.Globalization;

namespace PubPot.Web.Signups;

/// <summary>
/// Auswahl "Geplantes Release": "bereits veröffentlicht" oder ein Monat in den nächsten 3 Jahren.
/// Werte im Formular: "released" bzw. "2027-03".
/// </summary>
public static class ReleaseChoice
{
    public const string Released = "released";
    public const int MonthsAhead = 36;

    /// <summary>Auswählbare Monate ab dem aktuellen Monat, beschriftet in der aktuellen Sprache ("März 2027").</summary>
    public static IReadOnlyList<(string Value, string Label)> Months(DateTimeOffset now)
    {
        var first = new DateOnly(now.Year, now.Month, 1);
        return Enumerable.Range(0, MonthsAhead)
            .Select(i => first.AddMonths(i))
            .Select(m => (m.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                m.ToString("MMMM yyyy", CultureInfo.CurrentCulture)))
            .ToList();
    }

    public static bool TryParse(string? value, DateTimeOffset now, out bool released, out DateOnly? month)
    {
        released = false;
        month = null;

        if (value == Released)
        {
            released = true;
            return true;
        }

        if (!DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        var first = new DateOnly(now.Year, now.Month, 1);
        if (parsed < first || parsed >= first.AddMonths(MonthsAhead))
        {
            return false;
        }

        month = parsed;
        return true;
    }
}
