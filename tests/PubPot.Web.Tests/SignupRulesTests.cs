using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PubPot.Web.Forms;
using PubPot.Web.Signups;

namespace PubPot.Web.Tests;

/// <summary>Reine Regeln ohne Web-App und Datenbank: Steam-Link, E-Mail, Release-Auswahl, Einwilligung.</summary>
public class SignupRulesTests
{
    [Theory]
    [InlineData("https://store.steampowered.com/app/1234560/Sternenfeld/", 1234560)]
    [InlineData("http://store.steampowered.com/app/42", 42)]
    [InlineData("store.steampowered.com/app/42/", 42)]
    [InlineData("  https://STORE.steampowered.com/app/42?utm_source=x  ", 42)]
    [InlineData("https://store.steampowered.com/app/42#reviews", 42)]
    public void Steam_links_are_accepted(string link, long expectedId)
    {
        Assert.True(SteamLink.TryParse(link, out var id));
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://store.steampowered.com/")]
    [InlineData("https://store.steampowered.com/app/")]
    [InlineData("https://store.steampowered.com/app/abc")]
    [InlineData("https://store.steampowered.com/app/0")]
    [InlineData("https://store.steampowered.com.evil.example/app/42")]
    [InlineData("https://evil.example/?u=store.steampowered.com/app/42")]
    [InlineData("https://steamcommunity.com/app/42")]
    public void Other_links_are_rejected(string link) =>
        Assert.False(SteamLink.TryParse(link, out _));

    [Theory]
    [InlineData("dev@example.com", true)]
    [InlineData("vorname.nachname+pubpot@studio.example", true)]
    [InlineData("", true)] // leer prüft [Required]
    [InlineData("keine-mail", false)]
    [InlineData("a@b", false)]
    [InlineData("a b@example.com", false)]
    [InlineData("Name <dev@example.com>", false)]
    public void Email_validation(string email, bool valid) =>
        Assert.Equal(valid, new EmailAttribute().IsValid(email));

    [Fact]
    public void Release_choice_accepts_released_and_months_in_range()
    {
        var now = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.True(ReleaseChoice.TryParse("released", now, out var released, out var month));
        Assert.True(released);
        Assert.Null(month);

        Assert.True(ReleaseChoice.TryParse("2026-11", now, out _, out month));
        Assert.Equal(new DateOnly(2026, 11, 1), month);
        Assert.True(ReleaseChoice.TryParse("2029-10", now, out _, out _));   // letzter Monat (36 Monate)

        Assert.False(ReleaseChoice.TryParse("2026-10", now, out _, out _));  // Vergangenheit
        Assert.False(ReleaseChoice.TryParse("2029-11", now, out _, out _));  // zu weit weg
        Assert.False(ReleaseChoice.TryParse("2026-13", now, out _, out _));
        Assert.Equal(36, ReleaseChoice.Months(now).Count);
    }

    /// <summary>
    /// Ändert sich der Einwilligungstext, muss die Version steigen – sonst ist nicht mehr nachvollziehbar,
    /// welchem Wortlaut frühere Anmeldungen zugestimmt haben. Bei einer gewollten Änderung:
    /// ConsentText.Version erhöhen und hier Version und Hash eintragen.
    /// </summary>
    [Fact]
    public void Consent_text_changes_require_a_new_version()
    {
        var expected = new Dictionary<string, string>
        {
            ["2026-11-26"] = "723ea831bec30ec5def967ce6f99294a19aa97561af44594e29af4255587940a",
        };

        var resources = Path.Combine(TestSupport.WebProjectDir, "Resources");
        var text = new StringBuilder();
        foreach (var file in new[] { "SharedResource.resx", "SharedResource.en.resx" })
        {
            var values = XDocument.Load(Path.Combine(resources, file)).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
            foreach (var key in ConsentText.ResourceKeys)
            {
                text.Append(key).Append('=').Append(values[key]).Append('\n');
            }
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));

        Assert.True(expected.TryGetValue(ConsentText.Version, out var known),
            $"Für Version {ConsentText.Version} ist kein Hash hinterlegt. Aktueller Hash: {hash}");
        Assert.True(known == hash,
            $"Der Einwilligungstext hat sich geändert. ConsentText.Version erhöhen und Hash eintragen: {hash}");
    }
}

/// <summary>Kanal-Link oder Twitch-Login.</summary>
public class ChannelInputTests
{
    [Theory]
    [InlineData("kim_streamt", true)]
    [InlineData("Kim1", true)]
    [InlineData("https://www.youtube.com/@kim", true)]
    [InlineData("http://kick.com/kim", true)]
    [InlineData("kim", false)]                       // zu kurz für einen Twitch-Namen
    [InlineData("kim streamt", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://kim.example", false)]
    [InlineData("https://localhost/kim", false)]
    [InlineData("", false)]
    public void Channel_input(string input, bool valid) => Assert.Equal(valid, ChannelInput.IsValid(input));
}
