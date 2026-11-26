# PubPot

Landingpage mit Anmeldung für Entwickler und Creator, dazu Twitch-Worker, Docker, CI/CD und Monitoring.

> Im Aufbau. Plan und Status: [docs/ROADMAP.md](docs/ROADMAP.md)

## Aufbau

| Ordner | Inhalt |
|---|---|
| `src/PubPot.Web` | Landingpage (ASP.NET Core Razor Pages, Deutsch und Englisch) |
| `src/TwitchWorker` | Sammelt regelmäßig Stream-Daten von Twitch in PostgreSQL (.NET 10) |
| `tests/PubPot.Web.Tests` | Tests (xUnit v3) |
| `docs/` | Roadmap, Entscheidungen, Rückfragen, Journal, Checkliste |

## Bauen und testen

Voraussetzung: .NET SDK 11 (genaue Version in `global.json`). Die Projekte zielen auf .NET 10 (LTS).

```bash
dotnet build PubPot.slnx
dotnet test --solution PubPot.slnx
dotnet format PubPot.slnx --verify-no-changes
```

Alles auf einmal, so wie die CI prüft (bricht beim ersten Fehler ab): `./scripts/check.sh`.
Die Datenbank-Tests brauchen ein laufendes Docker, weil Testcontainers ein eigenes Postgres startet.

## Lokale Hilfsdienste (Docker)

Datenbank, Mail-Fänger und Datenbank-Oberfläche laufen in Docker:

```bash
cd deploy
cp .env.example .env        # einmalig, dann Passwörter eintragen
docker compose up -d
```

| Dienst | Adresse | Zweck |
|---|---|---|
| PostgreSQL 16 | `127.0.0.1:5432` | Datenbanken `pubpot` (Web) und `twitch` (Worker) mit eigenen Benutzern |
| Mailpit | http://localhost:8025 | fängt alle Mails ab, die die App lokal verschickt (SMTP auf Port 1025) |
| Adminer | http://localhost:8081 | Datenbank im Browser ansehen (Server: `db`) |

Alle Ports sind nur auf `127.0.0.1` gebunden, also nur vom eigenen Rechner aus erreichbar.
Stoppen: `docker compose down`. Alles inklusive Daten löschen: `docker compose down -v`.

## Website lokal starten

Einmalig die Verbindung zur lokalen Datenbank hinterlegen. Das Passwort ist `PUBPOT_DB_PASSWORD` aus `deploy/.env`.
Die User Secrets liegen in deinem Benutzerprofil, nicht im Repo:

```bash
cd src/PubPot.Web
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=pubpot;Username=pubpot;Password=<PUBPOT_DB_PASSWORD>"
dotnet tool restore            # installiert dotnet-ef in der Version aus dotnet-tools.json
dotnet ef database update      # legt die Tabellen an bzw. bringt sie auf den neuesten Stand
```

Dann starten:

```bash
dotnet run --project src/PubPot.Web
```

Danach http://localhost:5080 öffnen. `/` leitet je nach Browsersprache auf `/de` oder `/en` weiter.

### Admin-Zugang (optional)

Der Admin-Bereich ist aus, solange kein Passwort-Hash gesetzt ist. Hash erzeugen (das Passwort wird verdeckt abgefragt,
mindestens 12 Zeichen) und als User Secret hinterlegen. Der Befehl gibt die passende Zeile gleich mit aus:

```bash
dotnet run --project src/PubPot.Web -- hash-password
dotnet user-secrets set "Admin:PasswordHash" "<HASH>" --project src/PubPot.Web
```

| Einstellung | Umgebungsvariable | Bedeutung |
|---|---|---|
| `Admin:Username` | `Admin__Username` | Benutzername, Standard `admin` |
| `Admin:PasswordHash` | `Admin__PasswordHash` | Hash aus `hash-password`. Leer = Admin-Bereich aus. Ein ungültiger Wert verhindert den Start. |

> Ohne Startprofil muss die Umgebung über die Variable gesetzt werden: `ASPNETCORE_ENVIRONMENT=Development`.
> Nicht `dotnet run --environment Development` verwenden. Das setzt in .NET 10 eine Umgebungsvariable namens „Development“
> und startet die App im Production-Modus, ohne CSS (siehe `docs/ENTSCHEIDUNGEN.md`, E-016).
