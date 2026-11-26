using TwitchWorker;

var builder = Host.CreateApplicationBuilder(args);

// Einstellungen kommen aus appsettings.json und werden von Umgebungsvariablen überschrieben,
// z. B. Twitch__ClientId oder Worker__IntervalMinutes (doppelter Unterstrich = Abschnitt).
builder.Services.AddOptions<TwitchOptions>()
    .Bind(builder.Configuration.GetSection("Twitch"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId) && !string.IsNullOrWhiteSpace(o.ClientSecret),
        "Twitch__ClientId und Twitch__ClientSecret müssen gesetzt sein (siehe .env.example).")
    .ValidateOnStart();

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection("Worker"))
    .Validate(o => o.IntervalMinutes > 0, "Worker__IntervalMinutes muss größer als 0 sein.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings__Default fehlt.");

builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
builder.Services.AddSingleton<TwitchClient>();
builder.Services.AddSingleton(new SampleStore(connectionString));
builder.Services.AddHostedService<SnapshotWorker>();

builder.Build().Run();
