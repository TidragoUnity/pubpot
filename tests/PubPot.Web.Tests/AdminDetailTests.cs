using System.Net;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Detailseite einer Anmeldung: Anzeige aller Angaben und Pflege der Pipeline (Schritt 4.5).</summary>
[Collection("Database")]
public class AdminDetailTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);
    private string _cookie = "";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _cookie = await LoginAsAdminAsync(_client);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Guid> SeedAsync(Signup signup) =>
        await factory.WithDbAsync(async db =>
        {
            db.Signups.Add(signup);
            await db.SaveChangesAsync(Ct);
            return signup.Id;
        });

    private Task<Signup> LoadAsync(Guid id) =>
        factory.WithDbAsync(db => db.Signups.AsNoTracking().SingleAsync(s => s.Id == id, Ct));

    private async Task<string> DetailAsync(Guid id)
    {
        using var response = await GetAsync(_client, $"/admin/signups/{id}", cookie: _cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));
    }

    private Task<HttpResponseMessage> SaveAsync(Guid id, params KeyValuePair<string, string>[] fields) =>
        PostFormAsync(_client, $"/admin/signups/{id}", fields, cookie: _cookie);

    [Fact]
    public async Task Shows_all_developer_details()
    {
        var signup = TestData.Developer("dev@example.com", "Alex Studio");
        signup.Message = "Erste Zeile\nZweite Zeile";
        var id = await SeedAsync(signup);

        var html = await DetailAsync(id);

        foreach (var text in new[]
        {
            "Alex Studio", "dev@example.com", "Entwickler", "Englisch", "Pixel Forge", "Tiny Dungeon",
            "https://store.steampowered.com/app/480/", "März 2027", "Erste Zeile\nZweite Zeile", "Version test",
        })
        {
            Assert.Contains(text, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Shows_all_creator_details_and_warns_when_unconfirmed()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal", confirmed: false));

        var html = await DetailAsync(id);

        foreach (var text in new[] { "Kim Kanal", "Twitch, YouTube", "kim_streamt", "25–49", "Roguelikes, Cozy", "Noch nicht bestätigt" })
        {
            Assert.Contains(text, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Mentions_changes_that_wait_for_confirmation()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com"));
        await factory.WithDbAsync(async db =>
        {
            db.SignupConfirmations.Add(new SignupConfirmation
            {
                SignupId = id,
                TokenHash = new byte[32],
                CreatedAt = TestData.Now,
                ExpiresAt = TestData.Now.AddDays(30),
                PendingChanges = "{}",
            });
            return await db.SaveChangesAsync(Ct);
        });

        Assert.Contains("wartet auf die Bestätigung", await DetailAsync(id), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_updates_pipeline_and_counts_as_admin_activity_but_not_as_submission()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal"));
        var before = await LoadAsync(id);

        using var response = await SaveAsync(id,
            new("Status", "Pilot"), new("NextStepOn", "2026-12-03"), new("Note", "  Termin am Freitag.\r\nDemo zeigen.  "));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/admin/signups/{id}?saved=True", response.Headers.Location?.OriginalString);

        var after = await LoadAsync(id);
        Assert.Equal(PipelineStatus.Pilot, after.PipelineStatus);
        Assert.Equal(new DateOnly(2026, 12, 3), after.NextStepOn);
        Assert.Equal("Termin am Freitag.\nDemo zeigen.", after.AdminNote);
        Assert.NotNull(after.AdminUpdatedAt);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt); // Löschfrist unbestätigter Anmeldungen bleibt unberührt

        var html = await DetailAsync(id);
        Assert.Contains("Gespeichert.", await GetHtmlAsync(_client, $"/admin/signups/{id}?saved=True", cookie: _cookie), StringComparison.Ordinal);
        Assert.Contains("value=\"2026-12-03\"", html, StringComparison.Ordinal);
        Assert.Contains("Termin am Freitag.", html, StringComparison.Ordinal);
        Assert.Contains("03.12.2026", WebUtility.HtmlDecode(await GetHtmlAsync(_client, "/admin", cookie: _cookie)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_note_and_date_are_cleared()
    {
        var signup = TestData.Creator("kim@example.com");
        signup.AdminNote = "alt";
        signup.NextStepOn = new DateOnly(2026, 12, 1);
        var id = await SeedAsync(signup);

        using var response = await SaveAsync(id, new("Status", "Declined"), new("NextStepOn", ""), new("Note", "   "));

        var after = await LoadAsync(id);
        Assert.Equal(PipelineStatus.Declined, after.PipelineStatus);
        Assert.Null(after.AdminNote);
        Assert.Null(after.NextStepOn);
    }

    [Theory]
    [InlineData("Status", "Erfunden", "Bitte einen Status wählen.")]
    [InlineData("NextStepOn", "kein-datum", null)]
    [InlineData("NextStepOn", "1999-12-31", "Das Datum des nächsten Schritts ist ungültig.")]
    public async Task Invalid_input_is_rejected_and_nothing_is_saved(string field, string value, string? message)
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com"));
        var fields = new Dictionary<string, string> { ["Status"] = "Contacted", ["Note"] = "neu", ["NextStepOn"] = "" };
        fields[field] = value;

        using var response = await SaveAsync(id, [.. fields]);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("form-summary", html, StringComparison.Ordinal);
        if (message is not null)
        {
            Assert.Contains(message, html, StringComparison.Ordinal);
        }

        var after = await LoadAsync(id);
        Assert.Equal(PipelineStatus.New, after.PipelineStatus);
        Assert.Null(after.AdminUpdatedAt);
    }

    [Fact]
    public async Task Too_long_note_is_rejected()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com"));

        using var response = await SaveAsync(id, new("Status", "Contacted"), new("Note", new string('x', AppDbContext.AdminNoteMaxLength + 1)));

        Assert.Contains("höchstens 4000 Zeichen", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
        Assert.Null((await LoadAsync(id)).AdminNote);
    }

    [Theory]
    [InlineData("/admin/signups/0193f3c0-0000-7000-8000-00000000abcd")]
    [InlineData("/admin/signups/keine-guid")]
    public async Task Unknown_signup_is_not_found(string path)
    {
        using var response = await GetAsync(_client, path, cookie: _cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_links_to_the_detail_page_by_id_only()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal"));

        var html = await GetHtmlAsync(_client, "/admin", cookie: _cookie);

        Assert.Contains($"href=\"/admin/signups/{id}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_page_needs_login()
    {
        var id = await SeedAsync(TestData.Creator("kim@example.com"));

        using var response = await GetAsync(_client, $"/admin/signups/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/login", response.Headers.Location?.AbsolutePath);
    }
}
