using System.Net;
using Microsoft.EntityFrameworkCore;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>Löschen einzelner Anmeldungen für DSGVO-Löschanfragen (Schritt 4.6, E-028).</summary>
[Collection("Database")]
public class AdminDeleteTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);
    private string _cookie = "";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _cookie = await LoginAsAdminAsync(_client);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<int> SeedAsync(params object[] entities) =>
        factory.WithDbAsync(async db =>
        {
            db.AddRange(entities);
            return await db.SaveChangesAsync(Ct);
        });

    private static OutboxEmail Mail(string to, bool sent = false) => new()
    {
        To = to,
        Subject = "Test",
        TextBody = "Test",
        CreatedAt = TestData.Now,
        NextAttemptAt = TestData.Now,
        SentAt = sent ? TestData.Now : null,
    };

    private static SignupConfirmation Link(Signup signup) => new()
    {
        Signup = signup,
        TokenHash = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray(),
        CreatedAt = TestData.Now,
        ExpiresAt = TestData.Now.AddDays(30),
    };

    private Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        PostFormAsync(_client, $"/admin/signups/{id}/delete", [], cookie: _cookie);

    [Fact]
    public async Task Confirmation_page_asks_first_and_deletes_nothing()
    {
        var signup = TestData.Creator("kim@example.com", "Kim Kanal");
        await SeedAsync(signup);

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/admin/signups/{signup.Id}/delete", cookie: _cookie));

        Assert.Contains("Anmeldung löschen?", html, StringComparison.Ordinal);
        Assert.Contains("kim@example.com", html, StringComparison.Ordinal);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Deleting_removes_signup_links_and_all_mails_to_that_address()
    {
        var kim = TestData.Creator("kim@example.com", "Kim Kanal");
        var other = TestData.Developer("andere@example.com");
        // Groß-/Kleinschreibung in der Mail-Adresse ist egal; auch schon versendete Mails enthalten die Adresse.
        await SeedAsync(kim, other, Link(kim), Link(other),
            Mail("Kim@Example.com"), Mail("kim@example.com", sent: true), Mail("andere@example.com"));

        using var response = await DeleteAsync(kim.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin?deleted=True", response.Headers.Location?.OriginalString);
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal([other.Id], await db.Signups.Select(s => s.Id).ToListAsync(Ct));
            Assert.Equal([other.Id], await db.SignupConfirmations.Select(c => c.SignupId).ToListAsync(Ct));
            Assert.Equal(["andere@example.com"], await db.OutboxEmails.Select(m => m.To).ToListAsync(Ct));
            return 0;
        });

        var list = WebUtility.HtmlDecode(await GetHtmlAsync(_client, "/admin?deleted=True", cookie: _cookie));
        Assert.Contains("Die Anmeldung wurde gelöscht.", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Kim Kanal", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_address_in_another_role_is_pointed_out_and_its_mails_are_kept()
    {
        var asCreator = TestData.Creator("beides@example.com");
        var asDeveloper = TestData.Developer("beides@example.com");
        await SeedAsync(asCreator, asDeveloper, Mail("beides@example.com"));

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(_client, $"/admin/signups/{asCreator.Id}/delete", cookie: _cookie));
        Assert.Contains("Unter derselben E-Mail-Adresse gibt es noch eine Anmeldung", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/admin/signups/{asDeveloper.Id}\"", html, StringComparison.Ordinal);
        Assert.Contains("Anmeldung als Entwickler", html, StringComparison.Ordinal);

        using var response = await DeleteAsync(asCreator.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
        Assert.Equal(1, await factory.WithDbAsync(db => db.OutboxEmails.CountAsync(Ct)));
    }

    [Fact]
    public async Task Delete_needs_antiforgery_token()
    {
        var signup = TestData.Creator("kim@example.com");
        await SeedAsync(signup);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/signups/{signup.Id}/delete");
        request.Headers.Add("Cookie", _cookie);
        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Delete_needs_login()
    {
        var signup = TestData.Creator("kim@example.com");
        await SeedAsync(signup);

        using var response = await PostFormAsync(_client, $"/admin/signups/{signup.Id}/delete", [], tokenSource: "/admin/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/login", response.Headers.Location?.AbsolutePath);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Signups.CountAsync(Ct)));
    }

    [Fact]
    public async Task Unknown_signup_is_not_found()
    {
        var id = Guid.CreateVersion7();

        using var page = await GetAsync(_client, $"/admin/signups/{id}/delete", cookie: _cookie);
        using var post = await PostFormAsync(_client, $"/admin/signups/{id}/delete", [], tokenSource: "/admin", cookie: _cookie);

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Detail_page_links_to_the_confirmation()
    {
        var signup = TestData.Creator("kim@example.com");
        await SeedAsync(signup);

        var html = await GetHtmlAsync(_client, $"/admin/signups/{signup.Id}", cookie: _cookie);

        Assert.Contains($"href=\"/admin/signups/{signup.Id}/delete\"", html, StringComparison.Ordinal);
    }
}
