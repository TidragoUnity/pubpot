var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

// Liefert wwwroot aus, mit Komprimierung (gzip/brotli) und Fingerprinting zur Build-Zeit.
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
