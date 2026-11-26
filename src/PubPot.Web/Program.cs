using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using PubPot.Web;
using PubPot.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));

// --- Sprachen ---
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RouteOptions>(o =>
    o.ConstraintMap[CultureRouteConstraint.Name] = typeof(CultureRouteConstraint));
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// Leere 404-Antworten mit der Seite "/not-found" füllen (Statuscode bleibt 404).
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseRouting();
// Nach UseRouting, damit die Sprache aus der Route gelesen werden kann.
app.UseRequestLocalization();

// Liefert wwwroot aus, mit Komprimierung (gzip/brotli) und Fingerprinting zur Build-Zeit.
app.MapStaticAssets();

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
