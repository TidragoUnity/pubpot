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
