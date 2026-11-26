namespace PubPot.Web.Localization;

/// <summary>Die Sprachen der Website. Deutsch ist Standard (Sitz in Deutschland).</summary>
public static class SupportedCultures
{
    public const string German = "de";
    public const string English = "en";
    public const string Default = German;

    public static readonly IReadOnlyList<string> All = [German, English];

    public static bool IsSupported(string? culture) =>
        culture is not null && All.Contains(culture, StringComparer.OrdinalIgnoreCase);

    /// <summary>Die jeweils andere Sprache, z. B. für den Sprachumschalter.</summary>
    public static string Other(string culture) =>
        string.Equals(culture, German, StringComparison.OrdinalIgnoreCase) ? English : German;
}
