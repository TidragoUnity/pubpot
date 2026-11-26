using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PubPot.Web.Tests;

/// <summary>Gemeinsame Hilfen für die Tests.</summary>
internal static partial class TestSupport
{
    public static readonly string[] Cultures = ["de", "en"];

    /// <summary>Alle lokalisierten Seiten (ohne Sprachpräfix).</summary>
    public static readonly string[] PagePaths =
    [
        "",
        "/signup/developer",
        "/signup/creator",
        "/signup/thanks",
        "/signup/confirm",
        "/legal/imprint",
        "/legal/privacy",
    ];

    public static TheoryData<string> AllLocalizedPages()
    {
        var data = new TheoryData<string>();
        foreach (var culture in Cultures)
        {
            foreach (var path in PagePaths)
            {
                data.Add($"/{culture}{path}");
            }
        }

        return data;
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Client, der Weiterleitungen und Cookies NICHT automatisch verarbeitet – wir wollen sie sehen.</summary>
    public static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? baseAddress = null) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(baseAddress ?? "http://localhost"),
        });

    public static async Task<HttpResponseMessage> GetAsync(
        HttpClient client, string path, string? acceptLanguage = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request, Ct);
    }

    public static async Task<string> GetHtmlAsync(HttpClient client, string path, string? acceptLanguage = null, string? cookie = null)
    {
        using var response = await GetAsync(client, path, acceptLanguage, cookie);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    /// <summary>
    /// Schickt ein Formular ab wie ein Browser: erst die Seite laden (Antiforgery-Token und -Cookie holen),
    /// dann mit Token und Cookie per POST absenden.
    /// </summary>
    /// <remarks>
    /// <c>tokenSource</c>: Seite, von der das Token geholt wird (Standard: die Zielseite selbst).
    /// Antiforgery-Tokens gelten seitenübergreifend – nützlich, wenn die Zielseite selbst kein Formular zeigt.
    /// </remarks>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, IEnumerable<KeyValuePair<string, string>> fields, string? tokenSource = null,
        string? cookie = null)
    {
        // Mit Login-Cookie: Das Antiforgery-Token gehört dann zum angemeldeten Benutzer.
        using var page = await GetAsync(client, tokenSource ?? path, cookie: cookie);
        var html = await page.Content.ReadAsStringAsync(Ct);
        var token = AntiforgeryToken().Match(html).Groups[1].Value;
        var antiforgeryCookie = page.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal))
            .Split(';')[0];

        // Wie ein Browser: verstecktes Feld FormStarted (Spam-Schutz) mitschicken, falls der Test es nicht selbst setzt.
        var all = fields.ToList();
        if (all.All(f => f.Key != "FormStarted") && FormStarted().Match(html) is { Success: true } started)
        {
            all.Add(new("FormStarted", WebUtility.HtmlDecode(started.Groups[1].Value)));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(all.Append(new("__RequestVerificationToken", token))),
        };
        request.Headers.Add("Cookie", cookie is null ? antiforgeryCookie : $"{antiforgeryCookie}; {cookie}");
        return await client.SendAsync(request, Ct);
    }

    /// <summary>Meldet sich im Admin-Bereich an und gibt das Login-Cookie zurück ("Name=Wert").</summary>
    public static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        using var response = await PostFormAsync(client, "/admin/login",
            [new("Username", TestAdmin.Username), new("Password", TestAdmin.Password)]);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return AdminCookie(response) ?? throw new InvalidOperationException("Kein Admin-Cookie erhalten.");
    }

    /// <summary>Das Admin-Cookie aus einer Antwort ("Name=Wert"), oder <c>null</c>.</summary>
    public static string? AdminCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("__Host-pubpot-admin=", StringComparison.Ordinal)
                && !c.StartsWith("__Host-pubpot-admin=;", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex AntiforgeryToken();

    [GeneratedRegex(@"name=""FormStarted"" value=""([^""]+)""")]
    private static partial Regex FormStarted();

    /// <summary>Repo-Wurzel (dort liegt PubPot.slnx), um Quelldateien wie .resx und .css zu prüfen.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string WebProjectDir => Path.Combine(RepoRoot, "src", "PubPot.Web");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubPot.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("PubPot.slnx nicht gefunden.");
    }

    /// <summary>Alle href- und src-Attribute.</summary>
    [GeneratedRegex(@"\b(?:href|src)=""([^""]*)""")]
    public static partial Regex LinkAttribute();
}
