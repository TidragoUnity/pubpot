using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PubPot.Web.Tests;

/// <summary>Die Übersetzungen sind vollständig und sauber.</summary>
public partial class ResourceTests
{
    private static readonly string ResourcesDir = Path.Combine(TestSupport.WebProjectDir, "Resources");
    private static readonly string PagesDir = Path.Combine(TestSupport.WebProjectDir, "Pages");

    /// <summary>Alle deutschen (neutralen) .resx-Dateien, relativ zu Resources/.</summary>
    public static TheoryData<string> NeutralResourceFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(ResourcesDir, "*.resx", SearchOption.AllDirectories)
                     .Where(f => !f.EndsWith(".en.resx", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(ResourcesDir, file).Replace('\\', '/'));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NeutralResourceFiles))]
    public void English_translation_has_exactly_the_same_keys(string neutralFile)
    {
        var german = Read(Path.Combine(ResourcesDir, neutralFile));
        var englishPath = Path.Combine(ResourcesDir, neutralFile[..^".resx".Length] + ".en.resx");

        Assert.True(File.Exists(englishPath), $"Englische Datei fehlt: {englishPath}");
        var english = Read(englishPath);

        Assert.Equal(german.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(NeutralResourceFiles))]
    public void Texts_are_not_empty_and_contain_no_control_characters(string neutralFile)
    {
        var englishFile = neutralFile[..^".resx".Length] + ".en.resx";
        foreach (var file in new[] { neutralFile, englishFile })
        {
            foreach (var (key, value) in Read(Path.Combine(ResourcesDir, file)))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{file}: {key} ist leer.");
                Assert.False(value.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'),
                    $"{file}: {key} enthält Steuerzeichen.");
            }
        }
    }

    /// <summary>
    /// Jeder feste Schlüssel in einer View existiert in der passenden .resx:
    /// L["x"] in Pages/Foo/Bar.cshtml → Resources/Pages/Foo/Bar.resx, S["x"] → Resources/SharedResource.resx.
    /// (Zusammengesetzte Schlüssel wie L[$"How.{step}.Title"] prüft PageTests beim Rendern.)
    /// </summary>
    [Fact]
    public void Every_key_used_in_views_exists()
    {
        var shared = Read(Path.Combine(ResourcesDir, "SharedResource.resx"));
        var missing = new List<string>();

        foreach (var view in Directory.EnumerateFiles(PagesDir, "*.cshtml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(TestSupport.WebProjectDir, view);
            var pageResx = Path.Combine(ResourcesDir, Path.ChangeExtension(relative, ".resx"));
            var pageKeys = File.Exists(pageResx) ? Read(pageResx) : [];

            foreach (Match match in LiteralKey().Matches(File.ReadAllText(view)))
            {
                var (localizer, key) = (match.Groups[1].Value, match.Groups[2].Value);
                var known = localizer == "S" ? shared : pageKeys;
                if (!known.ContainsKey(key))
                {
                    missing.Add($"{relative}: {localizer}[\"{key}\"]");
                }
            }
        }

        Assert.Empty(missing);
    }

    private static Dictionary<string, string> Read(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    [GeneratedRegex("""\b([LS])\["([^"]+)"\]""")]
    private static partial Regex LiteralKey();
}
