using System.Globalization;
using System.Text.RegularExpressions;

namespace PubPot.Web.Tests;

/// <summary>
/// WCAG-AA-Kontraste für das helle und das dunkle Design, direkt aus den Design-Tokens in site.css.
/// Lighthouse prüft nur das helle Design – dieser Test deckt beide ab.
/// </summary>
public partial class ContrastTests
{
    private static readonly string Css = File.ReadAllText(
        Path.Combine(TestSupport.WebProjectDir, "wwwroot", "css", "site.css"));

    /// <summary>(Vordergrund, Hintergrund, Mindestkontrast, Verwendung). Text: 4,5:1, Fokusrahmen: 3:1.</summary>
    public static TheoryData<string, string, double, string> Pairs() => new()
    {
        { "text", "bg", 4.5, "Fließtext" },
        { "text", "surface", 4.5, "Text auf Karten" },
        { "text", "surface-2", 4.5, "Text in grauen Abschnitten" },
        { "text-muted", "bg", 4.5, "Nebentext" },
        { "text-muted", "surface", 4.5, "Nebentext auf Karten" },
        { "text-muted", "surface-2", 4.5, "Nebentext in grauen Abschnitten" },
        { "primary", "bg", 4.5, "Links" },
        { "primary", "surface", 4.5, "Links auf Karten" },
        { "primary", "surface-2", 4.5, "Links und Outline-Buttons in grauen Abschnitten" },
        { "on-primary", "primary", 4.5, "Button-Text" },
        { "on-primary", "primary-hover", 4.5, "Button-Text beim Überfahren" },
        { "accent", "bg", 4.5, "Eyebrow-Text" },
        { "accent", "surface", 4.5, "Datum im Pilot-Kasten" },
        { "accent", "surface-2", 4.5, "Eyebrow in grauen Abschnitten" },
        { "text", "accent-soft", 4.5, "TODO-Box und Beispiel-Badge" },
        { "focus", "bg", 3.0, "Fokusrahmen" },
        { "focus", "surface-2", 3.0, "Fokusrahmen in grauen Abschnitten" },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Light_theme_meets_wcag_aa(string foreground, string background, double minimum, string usage) =>
        AssertContrast(LightTokens(), foreground, background, minimum, usage);

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Dark_theme_meets_wcag_aa(string foreground, string background, double minimum, string usage) =>
        AssertContrast(DarkTokens(), foreground, background, minimum, usage);

    /// <summary>Die Dunkel-Farben stehen zweimal im CSS (Systemeinstellung und Schalter) und müssen gleich sein.</summary>
    [Fact]
    public void Dark_tokens_are_identical_for_system_setting_and_toggle()
    {
        var system = Tokens(Block(DarkSystemBlock()));

        Assert.Equal(DarkTokens(), system);
    }

    private static void AssertContrast(Dictionary<string, string> tokens, string fg, string bg, double minimum, string usage)
    {
        var ratio = Contrast(tokens[fg], tokens[bg]);
        Assert.True(ratio >= minimum,
            $"{usage}: --{fg} {tokens[fg]} auf --{bg} {tokens[bg]} hat nur {ratio:F2}:1 (mindestens {minimum}:1).");
    }

    private static Dictionary<string, string> LightTokens() => Tokens(Block(LightBlock()));

    private static Dictionary<string, string> DarkTokens() => Tokens(Block(DarkToggleBlock()));

    private static string Block(Regex regex)
    {
        var match = regex.Match(Css);
        Assert.True(match.Success, $"CSS-Block nicht gefunden: {regex}");
        return match.Groups["body"].Value;
    }

    private static Dictionary<string, string> Tokens(string block) =>
        ColorToken().Matches(block).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToLowerInvariant());

    /// <summary>Kontrastverhältnis nach WCAG 2.x.</summary>
    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var c = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }

    // Der erste ":root { … }"-Block ist das helle Design.
    [GeneratedRegex(@"^:root\s*\{(?<body>[^}]*)\}", RegexOptions.Multiline)]
    private static partial Regex LightBlock();

    [GeneratedRegex(@":root\[data-theme=""dark""\]\s*\{(?<body>[^}]*)\}")]
    private static partial Regex DarkToggleBlock();

    [GeneratedRegex(@":root:not\(\[data-theme=""light""\]\)\s*\{(?<body>[^}]*)\}")]
    private static partial Regex DarkSystemBlock();

    // Nur reine Hex-Farben (#rrggbb); Schatten und rgb()-Werte sind nicht relevant.
    [GeneratedRegex(@"--([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})\s*;")]
    private static partial Regex ColorToken();
}
