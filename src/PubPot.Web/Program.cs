using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using PubPot.Web;
using PubPot.Web.Localization;
using PubPot.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));

// --- Sprachen ---
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RouteOptions>(o =>
{
    o.ConstraintMap[CultureRouteConstraint.Name] = typeof(CultureRouteConstraint);
    // Erzeugte Links klein schreiben: /de/signup/developer statt /de/Signup/Developer.
    o.LowercaseUrls = true;
});
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = SupportedCultures.All.Select(CultureInfo.GetCultureInfo).ToList();
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
    o.DefaultRequestCulture = new RequestCulture(SupportedCultures.Default);

    // 1. Sprache aus der URL (/de/..., /en/...). 2. Für Seiten ohne Präfix (Fehler, 404): gewählte Sprache,
    // 3. sonst Browsersprache.
    o.RequestCultureProviders =
    [
        new RouteDataRequestCultureProvider
        {
            RouteDataStringKey = CultureRouteConvention.RouteKey,
            UIRouteDataStringKey = CultureRouteConvention.RouteKey,
        },
        new CustomRequestCultureProvider(ctx =>
            Task.FromResult(LanguagePreference.FromCookie(ctx) is { } c ? new ProviderCultureResult(c) : null)),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentLanguage>();

builder.Services.AddRazorPages(o => o.Conventions.Add(new CultureRouteConvention()))
    .AddViewLocalization();

// --- Sicherheit ---
// Kein "Server: Kestrel"-Header: verrät unnötig, welche Software läuft.
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Für alle Cookies (auch künftige wie Antiforgery und Admin-Login): HttpOnly, SameSite=Lax,
// Secure bei HTTPS. Hinter Caddy kommt "HTTPS" über X-Forwarded-Proto an (wird in Phase 5 eingerichtet).
builder.Services.Configure<CookiePolicyOptions>(o =>
{
    o.MinimumSameSitePolicy = SameSiteMode.Lax;
    o.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    o.Secure = CookieSecurePolicy.SameAsRequest;
});

// HSTS: Browser merken sich ein Jahr lang, die Seite nur per HTTPS aufzurufen.
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
});

// --- Betrieb ---
// /health = "Prozess lebt" (Liveness). /health/ready mit Datenbank-Prüfung folgt in Phase 3.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCookiePolicy();

// Leere 404-Antworten mit der Seite "/not-found" füllen (Statuscode bleibt 404).
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseRouting();
// Nach UseRouting, damit die Sprache aus der Route gelesen werden kann.
app.UseRequestLocalization();

// Liefert wwwroot aus, mit Komprimierung (gzip/brotli) und Fingerprinting zur Build-Zeit.
app.MapStaticAssets();

app.MapHealthChecks("/health");

// "/" leitet je nach gewählter Sprache bzw. Browsersprache auf /de oder /en weiter.
app.MapGet("/", (HttpContext ctx) =>
{
    ctx.Response.Headers.Vary = "Accept-Language, Cookie";
    ctx.Response.Headers.CacheControl = "no-store";
    return Results.Redirect("/" + LanguagePreference.Resolve(ctx));
});

// Sprachumschalter: merkt sich die Wahl und führt zur selben Seite in der anderen Sprache.
app.MapGet("/lang/{culture}", (string culture, string? returnPath, HttpContext ctx) =>
{
    if (!SupportedCultures.IsSupported(culture))
    {
        return Results.NotFound();
    }

    LanguagePreference.Remember(ctx, culture);
    return Results.LocalRedirect(LanguagePreference.TranslatePath(returnPath, culture.ToLowerInvariant()));
});

app.MapRazorPages().WithStaticAssets();

app.Run();
