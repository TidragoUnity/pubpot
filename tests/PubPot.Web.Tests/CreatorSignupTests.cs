using System.Net;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using PubPot.Web.Signups;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Creator-Formular gegen eine echte Datenbank.</summary>
[Collection("Database")]
public class CreatorSignupTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static List<KeyValuePair<string, string>> ValidForm(string email = "creator@example.com") =>
    [
        new("Input.Name", "Kim Streamt"),
        new("Input.Email", email),
        new("Input.Platforms", "Twitch"),
        new("Input.Platforms", "YouTube"),   // Mehrfachauswahl: derselbe Name mehrmals
        new("Input.Channel", "kim_streamt"),
        new("Input.AudienceSize", "From25To49"),
        new("Input.Genres", "Roguelikes, Cozy Games"),
        new("Input.Adult", "true"),
        new("Input.Consent", "true"),
    ];

    private static List<KeyValuePair<string, string>> With(string field, string value, string email = "creator@example.com") =>
        ValidForm(email).Where(kv => kv.Key != field).Append(new(field, value)).ToList();

    [Fact]
    public async Task Valid_signup_is_saved_with_all_platforms()
    {
        using var response = await PostFormAsync(_client, "/en/signup/creator", ValidForm());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en/signup/thanks", response.Headers.Location?.OriginalString);

        var saved = Assert.Single(await factory.WithDbAsync(db => db.Signups.OfType<CreatorSignup>().ToListAsync(Ct)));
        Assert.Equal(CreatorPlatforms.Twitch | CreatorPlatforms.YouTube, saved.Platforms);
        Assert.Equal("kim_streamt", saved.Channel);
        Assert.Equal(AudienceSize.From25To49, saved.AudienceSize);
        Assert.Equal("Roguelikes, Cozy Games", saved.Genres);
        Assert.True(saved.ConfirmedAdult);
        Assert.Equal("en", saved.Language);
        Assert.Equal(ConsentText.Version, saved.ConsentVersion);
    }

    [Theory]
    [InlineData("https://www.youtube.com/@kimstreamt")]
    [InlineData("https://twitch.tv/kim_streamt")]
    [InlineData("kim_streamt")]
    public async Task Channel_link_or_twitch_login_is_accepted(string channel)
    {
        using var response = await PostFormAsync(_client, "/de/signup/creator", With("Input.Channel", channel));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Theory]
    [InlineData("Input.Platforms", "Myspace", "Bitte prüfe diese Angabe.")]  // unbekannter Wert → Modellbindung greift
    [InlineData("Input.Channel", "kim streamt!", "Link (https://…) oder deinen Twitch-Namen")]
    [InlineData("Input.Channel", "ftp://kim.example", "Link (https://…) oder deinen Twitch-Namen")]
    [InlineData("Input.AudienceSize", "Millionen", "Bitte prüfe diese Angabe.")]
    [InlineData("Input.Adult", "false", "Mitmachen kannst du erst ab 18 Jahren.")]
    [InlineData("Input.Consent", "false", "Ohne deine Einwilligung können wir dich nicht kontaktieren.")]
    public async Task Invalid_values_are_rejected(string field, string value, string expectedError)
    {
        var form = field == "Input.Platforms"
            ? ValidForm().Where(kv => kv.Key != field).Append(new(field, value)).ToList()
            : With(field, value);

        using var response = await PostFormAsync(_client, "/de/signup/creator", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Validation.", html, StringComparison.Ordinal);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task No_platform_selected_is_rejected()
    {
        var form = ValidForm().Where(kv => kv.Key != "Input.Platforms").ToList();

        using var response = await PostFormAsync(_client, "/de/signup/creator", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.Contains("Bitte wähle mindestens eine Plattform.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"Input_Platforms-error\"", html, StringComparison.Ordinal);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Same_email_as_developer_and_creator_gives_two_signups()
    {
        await PostFormAsync(_client, "/de/signup/creator", ValidForm("both@example.com"));
        await PostFormAsync(_client, "/de/signup/developer", new Dictionary<string, string>
        {
            ["Input.Name"] = "Kim",
            ["Input.Email"] = "both@example.com",
            ["Input.Studio"] = "Kim Games",
            ["Input.GameTitle"] = "Spiel",
            ["Input.SteamLink"] = "https://store.steampowered.com/app/42",
            ["Input.Release"] = ReleaseChoice.Released,
            ["Input.HasDemo"] = "false",
            ["Input.NextFestPilot"] = "false",
            ["Input.HasPublisher"] = "false",
            ["Input.Consent"] = "true",
        });

        // Im Speicher sortieren: In der Datenbank steht die Rolle als Text ("Creator" < "Developer").
        var kinds = (await factory.WithDbAsync(db => db.Signups.Select(s => s.Kind).ToListAsync(Ct))).Order().ToList();
        Assert.Equal([SignupKind.Developer, SignupKind.Creator], kinds);
    }

    [Fact]
    public async Task Selected_platforms_stay_checked_after_validation_error()
    {
        var form = With("Input.Genres", "");

        using var response = await PostFormAsync(_client, "/de/signup/creator", form);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Matches("id=\"Platform-Twitch\"[^>]*checked", html);
        Assert.Matches("id=\"Platform-YouTube\"[^>]*checked", html);
        Assert.DoesNotMatch("id=\"Platform-Kick\"[^>]*checked", html);
    }
}
