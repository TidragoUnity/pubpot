using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PubPot.Web;
using PubPot.Web.Admin;
using PubPot.Web.Data;
using PubPot.Web.Email;
using PubPot.Web.Forms;
using PubPot.Web.Localization;
using PubPot.Web.Notifications;
using PubPot.Web.Security;
using PubPot.Web.Signups;
using PubPot.Web.Spam;

// Hilfsbefehl statt Webserver: dotnet run --project src/PubPot.Web -- hash-password
if (args is [HashPasswordCommand.Name, ..])
{
    return HashPasswordCommand.RunWithConsole();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));

// --- Datenbank ---
// Lokal aus den User Secrets, in Docker aus der Umgebungsvariable ConnectionStrings__Default.
// Fehlt sie, bricht die App sofort ab ("fail fast") statt erst bei der ersten Anmeldung.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Default fehlt. Lokal: siehe README (dotnet user-secrets), in Docker: ConnectionStrings__Default.");
}

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());
builder.Services.AddSingleton(TimeProvider.System);

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

builder.Services.AddRazorPages(o =>
    {
        o.Conventions.Add(new CultureRouteConvention());
        // Jede Seite unter Pages/Admin verlangt den Admin-Login, außer der Login-Seite selbst.
        o.Conventions.AuthorizeFolder("/Admin", AdminAuth.Policy);
        o.Conventions.AllowAnonymousToPage("/Admin/Login");
    })
    .AddViewLocalization()
    // Fehlermeldungen und Feldnamen der Formulare ([Required(ErrorMessage = "Validation.Required")] usw.)
    // sind Schlüssel in SharedResource.resx.
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)))
    // Keine data-val-*-Attribute: Wir prüfen ohne jQuery-Validation (Browser + Server).
    .AddViewOptions(o => o.HtmlHelperOptions.ClientValidationEnabled = false);
builder.Services.AddSingleton<IConfigureOptions<MvcOptions>, LocalizedModelBindingMessages>();
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, LocalizedValidationAdapterProvider>();

// --- Anmeldungen und E-Mail ---
builder.Services.AddScoped<LinkBuilder>();
builder.Services.AddScoped<SignupService>();

// Außerhalb der Entwicklung muss die öffentliche Adresse feststehen: Links in Mails dürfen nie aus dem
// Host-Header der Anfrage entstehen (sonst könnten Angreifer Links auf fremde Seiten in echte Mails schmuggeln).
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Site:BaseUrl"]))
{
    throw new InvalidOperationException("Site:BaseUrl fehlt (z. B. Site__BaseUrl=https://pubpot.games).");
}

// SMTP-Zugang wird beim Start geprüft (fail fast), nicht erst beim ersten Mailversand.
builder.Services.AddOptions<SmtpOptions>().Bind(builder.Configuration.GetSection("Smtp"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OutboxOptions>().Bind(builder.Configuration.GetSection("Outbox"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddScoped<IEmailTransport, SmtpEmailTransport>();
builder.Services.AddScoped<OutboxProcessor>();
if (builder.Configuration.GetValue("Outbox:Enabled", defaultValue: true))
{
    builder.Services.AddHostedService<OutboxSenderService>();
}

// Hinweise ans Team bei neuen bestätigten Anmeldungen: Mail über die Outbox, Discord nur mit Webhook-URL (E-030).
builder.Services.AddOptions<NotificationOptions>().Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .Validate(NotificationOptions.IsValid, "Notifications:Email ist keine gültige Adresse oder Notifications:DiscordWebhookUrl keine https://discord.com/api/webhooks/…-Adresse.")
    .ValidateOnStart();
builder.Services.AddScoped<SignupNotifier>();
builder.Services.AddSingleton<DiscordQueue>();
// RemoveAllLoggers: Sonst schreibt HttpClient jede URL ins Log, und die Webhook-URL ist ein Geheimnis.
builder.Services.AddHttpClient(DiscordSenderService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10))
    .RemoveAllLoggers();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Notifications:DiscordWebhookUrl"]))
{
    builder.Services.AddHostedService<DiscordSenderService>();
}

// Aufräumen nach Aufbewahrungsfristen (unbestätigte Anmeldungen nach 30 Tagen, versendete Mails nach 7 Tagen).
builder.Services.AddOptions<CleanupOptions>().Bind(builder.Configuration.GetSection("Cleanup")).ValidateOnStart();
builder.Services.AddScoped<DataCleanup>();
if (builder.Configuration.GetValue("Cleanup:Enabled", defaultValue: true))
{
    builder.Services.AddHostedService<DataCleanupService>();
}

// --- Admin-Bereich ---
// Ein Konto, Passwort nur als Hash aus der Umgebungsvariable Admin__PasswordHash. Ohne Hash ist der Bereich aus.
// Ein kaputter Hash (z. B. beim Kopieren abgeschnitten) bricht den Start ab, statt später jeden Login abzulehnen.
builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName))
    .Validate(AdminOptions.IsValid, "Admin:Username fehlt oder Admin:PasswordHash ist kein gültiger Hash (neu erzeugen mit: hash-password).")
    .ValidateOnStart();
builder.Services.AddAdminAuthentication();
builder.Services.AddScoped<AdminSignups>();

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

// Data Protection signiert/verschlüsselt Antiforgery-Tokens, Formular-Zeitstempel und später das Admin-Login.
// Die Schlüssel liegen in der Datenbank – im Container gingen sie bei jedem Neustart verloren.
builder.Services.AddDataProtection()
    .SetApplicationName("PubPot")
    .PersistKeysToDbContext<AppDbContext>();

// Spam-Schutz ohne Drittanbieter: Honeypot, Mindestausfüllzeit, Rate-Limiting, Sperrfrist für Mails.
var spamSection = builder.Configuration.GetSection("Spam");
builder.Services.AddOptions<SpamOptions>().Bind(spamSection).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<FormTiming>();
builder.Services.AddFormRateLimiting(
    spamSection.Get<SpamOptions>() ?? new SpamOptions(),
    builder.Configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions());

// HSTS: Browser merken sich ein Jahr lang, die Seite nur per HTTPS aufzurufen.
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
});

// --- Betrieb ---
// /health       = "Prozess lebt" (Liveness, ohne Prüfungen) – Docker startet den Container neu, wenn das ausfällt.
// /health/ready = "kann arbeiten" (Readiness): Datenbank erreichbar und alle Migrationen angewendet.
//                 Dagegen läuft der Smoke-Test nach dem Deploy.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<MigrationsHealthCheck>("migrations", tags: ["ready"]);

var app = builder.Build();

if (!app.Services.GetRequiredService<IOptions<AdminOptions>>().Value.IsEnabled)
{
    app.Logger.LogWarning("Admin-Bereich ist aus, weil Admin:PasswordHash fehlt.");
}

if (!app.Services.GetRequiredService<IOptions<NotificationOptions>>().Value.EmailEnabled)
{
    app.Logger.LogWarning("Keine Hinweis-Mails bei neuen Anmeldungen, weil Notifications:Email fehlt.");
}

app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCookiePolicy();

// Leere Fehlerantworten (404, 429, 400 …) mit einer freundlichen Seite füllen; der Statuscode bleibt erhalten.
app.UseStatusCodePagesWithReExecute("/status/{0}");

app.UseRouting();
// Nach UseRouting, damit die Sprache aus der Route gelesen werden kann.
app.UseRequestLocalization();
app.UseRateLimiter();
// Admin-Bereich: 404 ohne Passwort-Hash, sonst Login per Cookie (E-026).
app.UseAdminGate();
app.UseAuthentication();
app.UseAuthorization();

// Liefert wwwroot aus, mit Komprimierung (gzip/brotli) und Fingerprinting zur Build-Zeit.
app.MapStaticAssets();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

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
return 0;
