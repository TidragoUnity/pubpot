using System.Net;
using System.Text;
using PubPot.Web.Admin;
using PubPot.Web.Data;
using static PubPot.Web.Tests.TestSupport;

namespace PubPot.Web.Tests;

/// <summary>CSV-Export der gefilterten Liste (Schritt 4.7, E-029).</summary>
[Collection("Database")]
public class AdminExportTests(DatabaseWebFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = CreateClient(factory);
    private string _cookie = "";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _cookie = await LoginAsAdminAsync(_client);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task SeedAsync(params Signup[] signups) =>
        factory.WithDbAsync(async db =>
        {
            db.Signups.AddRange(signups);
            return await db.SaveChangesAsync(Ct);
        });

    private async Task<(HttpResponseMessage Response, byte[] Bytes, string[] Lines)> ExportAsync(
        params KeyValuePair<string, string>[] fields)
    {
        var response = await PostFormAsync(_client, "/admin/export", fields, tokenSource: "/admin", cookie: _cookie);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        var text = Encoding.UTF8.GetString(bytes, 3, Math.Max(0, bytes.Length - 3));
        return (response, bytes, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task Export_is_an_excel_friendly_csv_download()
    {
        await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal"), TestData.Developer("dev@example.com", "Alex Studio"));

        var (response, bytes, lines) = await ExportAsync();
        using var _ = response;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Matches(@"^pubpot-anmeldungen-\d{4}-\d{2}-\d{2}\.csv$", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // BOM für Excel

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("ID;Art;Name;E-Mail;Sprache;Eingegangen", lines[0], StringComparison.Ordinal);
        var kim = lines.Single(l => l.Contains("Kim Kanal", StringComparison.Ordinal));
        Assert.Contains(";Creator;Kim Kanal;kim@example.com;de;", kim, StringComparison.Ordinal);
        Assert.Contains(";neu;", kim, StringComparison.Ordinal);
        Assert.Contains("Twitch, YouTube;kim_streamt;25–49;Roguelikes, Cozy;ja;", kim, StringComparison.Ordinal);
        var alex = lines.Single(l => l.Contains("Alex Studio", StringComparison.Ordinal));
        Assert.Contains("Pixel Forge;Tiny Dungeon;480;März 2027;ja;ja;nein;", alex, StringComparison.Ordinal);
        Assert.Equal(lines[0].Split(';').Length, alex.Split(';').Length);
    }

    [Fact]
    public async Task Export_uses_the_same_filters_and_search_as_the_list()
    {
        var contacted = TestData.Developer("pixel@example.com", "Paula Pixel");
        contacted.PipelineStatus = PipelineStatus.Contacted;
        await SeedAsync(TestData.Creator("kim@example.com", "Kim Kanal"), contacted, TestData.Developer("x@example.com", "Xaver"));

        var (r1, _, byKind) = await ExportAsync(new("Kind", "Developer"), new("Status", "Contacted"));
        var (r2, _, bySearch) = await ExportAsync(new KeyValuePair<string, string>("Search", "kim_"));
        r1.Dispose();
        r2.Dispose();

        Assert.Equal(2, byKind.Length);
        Assert.Contains("Paula Pixel", byKind[1], StringComparison.Ordinal);
        Assert.Equal(2, bySearch.Length);
        Assert.Contains("Kim Kanal", bySearch[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formulas_are_defused_and_special_characters_are_quoted()
    {
        var evil = TestData.Creator("evil@example.com", "=HYPERLINK(\"https://boese.example\")");
        evil.Message = "Zeile 1; mit Semikolon\nZeile 2 mit \"Zitat\"";
        evil.Genres = "@SUM(1+1)";
        await SeedAsync(evil);

        var (response, bytes, _) = await ExportAsync();
        using var _r = response;
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Contains(";\"'=HYPERLINK(\"\"https://boese.example\"\")\";", text, StringComparison.Ordinal);
        Assert.Contains(";'@SUM(1+1);", text, StringComparison.Ordinal);
        Assert.Contains("\"Zeile 1; mit Semikolon\nZeile 2 mit \"\"Zitat\"\"\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Kim", "Kim")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+49 123", "'+49 123")]
    [InlineData("-2", "'-2")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tTab", "'\tTab")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("sag \"hi\"", "\"sag \"\"hi\"\"\"")]
    [InlineData("Zeile\nZwei", "\"Zeile\nZwei\"")]
    public void Cells_are_escaped(string? value, string expected)
    {
        Assert.Equal(expected, SignupCsv.Cell(value));
    }

    [Fact]
    public async Task List_offers_the_export_inside_the_filter_form()
    {
        var html = await GetHtmlAsync(_client, "/admin", cookie: _cookie);

        Assert.Contains("formaction=\"/admin/export\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_redirects_to_the_list()
    {
        using var response = await GetAsync(_client, "/admin/export", cookie: _cookie);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Export_needs_login_and_antiforgery_token()
    {
        await SeedAsync(TestData.Creator("kim@example.com"));

        using var anonymous = await PostFormAsync(_client, "/admin/export", [], tokenSource: "/admin/login");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/export");
        request.Headers.Add("Cookie", _cookie);
        using var withoutToken = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/admin/login", anonymous.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
    }
}
