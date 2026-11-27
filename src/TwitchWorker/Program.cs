using TwitchWorker;

// Docker-Healthcheck im Container ohne Shell: dotnet TwitchWorker.dll healthcheck (E-036)
if (args is [HealthCheckCommand.Name])
{
    return HealthCheckCommand.Run();
}

var builder = Host.CreateApplicationBuilder(args);

// Einstellungen kommen aus appsettings.json und werden von Umgebungsvariablen überschrieben,
// z. B. Twitch__ClientId oder Worker__IntervalMinutes (doppelter Unterstrich = Abschnitt).
builder.Services.AddOptions<TwitchOptions>()
    .Bind(builder.Configuration.GetSection("Twitch"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId) && !string.IsNullOrWhiteSpace(o.ClientSecret),
        "Twitch__ClientId und Twitch__ClientSecret müssen gesetzt sein (siehe deploy/.env.example).")
    .ValidateOnStart();

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection("Worker"))
    .Validate(o => o.IntervalMinutes > 0, "Worker__IntervalMinutes muss größer als 0 sein.")
    .Validate(o => PushNotifier.IsValid(o.PushUrl), "Worker__PushUrl muss leer oder eine http(s)-Adresse sein.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default fehlt.");

builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
builder.Services.AddSingleton<TwitchClient>();
builder.Services.AddSingleton(new SampleStore(connectionString));
// Eigener HttpClient mit kurzem Timeout: Ein hängender Monitor-Dienst soll die nächste Runde nicht aufhalten.
builder.Services.AddSingleton(sp => new PushNotifier(
    new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>(),
    sp.GetRequiredService<ILogger<PushNotifier>>()));
// Messwerte für Prometheus: /metrics auf Port 9464, nur im internen Docker-Netz (E-052).
builder.Services.AddWorkerMetrics(builder.Configuration);
builder.Services.AddHostedService<SnapshotWorker>();

builder.Build().Run();
return 0;
