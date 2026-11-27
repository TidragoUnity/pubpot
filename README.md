# PubPot

[![CI](https://github.com/TidragoUnity/pubpot/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/TidragoUnity/pubpot/actions/workflows/ci.yml)
[![CodeQL](https://github.com/TidragoUnity/pubpot/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/TidragoUnity/pubpot/actions/workflows/codeql.yml)

Landingpage mit Anmeldung für Entwickler und Creator, dazu Twitch-Worker, Docker, CI/CD und Monitoring.

> Im Aufbau. Plan und Status: [docs/ROADMAP.md](docs/ROADMAP.md)

## Aufbau

| Ordner | Inhalt |
|---|---|
| `src/PubPot.Web` | Landingpage (ASP.NET Core Razor Pages, Deutsch und Englisch) |
| `src/TwitchWorker` | Sammelt regelmäßig Stream-Daten von Twitch in PostgreSQL (.NET 10) |
| `tests/PubPot.Web.Tests` | Tests der Website (xUnit v3, Testcontainers) |
| `tests/TwitchWorker.Tests` | Tests des Workers (Healthcheck, Schema-Skripte im echten Postgres) |
| `deploy/` | Compose-Dateien, Caddyfile, Datenbank-Skripte |
| `.github/` | CI-Workflows, Dependabot, Regeln für `main` |
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

## CI und Arbeitsablauf

Jede Änderung kommt über einen **Pull Request** auf `main`. Direkt pushen geht nicht (Regeln in
[`.github/rulesets/main.json`](.github/rulesets/main.json)). Ein PR lässt sich erst mergen, wenn alle Prüfungen grün
sind, und zwar nur per „Squash and merge“: ein Commit pro Änderung.

| Workflow | Wann | Was |
|---|---|---|
| [CI](.github/workflows/ci.yml): Build und Tests | jeder PR, `main`, montags | `./scripts/check.sh` wie lokal: Build mit Warnungen als Fehler, Format, alle Tests |
| CI: Images und Smoke-Test | ebenso | baut die drei Images, startet den Stack wie auf dem Server (`./scripts/smoke-test.sh`), Trivy-Scan |
| CI: Push nach GHCR | nur nach Merge auf `main` | Images mit Tag `<commit-sha>` und `main` nach `ghcr.io/tidragounity/pubpot-{web,migrate,worker}`, mit SBOM und Provenance |
| [CodeQL](.github/workflows/codeql.yml) | jeder PR, `main`, mittwochs | Sicherheitsanalyse für C#, Workflows und JavaScript |
| [Dependabot](.github/dependabot.yml) | montags | Update-PRs für NuGet, SDK, Docker-Images und Actions |

Ablauf für eine Änderung:

```bash
git switch -c 6.8-kurzname          # Branch pro Roadmap-Schritt
# … ändern, ./scripts/check.sh …
git push -u origin 6.8-kurzname
gh pr create --fill                 # PR öffnen, die CI startet
gh pr checks --watch                # warten, bis alles grün ist
gh pr merge --squash --delete-branch
```

Die fertigen Images sind öffentlich: `docker pull ghcr.io/tidragounity/pubpot-web:main`.

## Lokale Hilfsdienste (Docker)

Datenbank, Mail-Fänger und Datenbank-Oberfläche laufen in Docker. Für die Entwicklung mit `dotnet run`
nur diese drei Dienste starten:

```bash
cd deploy
cp .env.example .env        # einmalig, dann Passwörter eintragen
docker compose up -d db mailpit adminer
```

Ein einfaches `docker compose up -d --build` startet zusätzlich die Web-App als Container, hinter Caddy unter
https://localhost (die Zertifikatswarnung des Browsers einmal wegklicken, F-017). **Nicht beides gleichzeitig** gegen dieselbe Datenbank
laufen lassen: Zwei Instanzen würden sich die Hintergrund-Jobs teilen und Mails doppelt verschicken (E-037).

| Dienst | Adresse | Zweck |
|---|---|---|
| PostgreSQL 16 | `127.0.0.1:5432` | Datenbanken `pubpot` (Web) und `twitch` (Worker) mit eigenen Benutzern |
| Mailpit | http://localhost:8025 | fängt alle Mails ab, die die App lokal verschickt (SMTP auf Port 1025) |
| Adminer | http://localhost:8081 | Datenbank im Browser ansehen (Server: `db`) |

Alle Ports sind nur auf `127.0.0.1` gebunden, also nur vom eigenen Rechner aus erreichbar.
Stoppen: `docker compose down`. Alles inklusive Daten löschen: `docker compose down -v` (auch die Daten des Workers, beide Datenbanken liegen im selben Volume).

## Alles in Docker starten

So läuft die Anwendung wie später auf dem Server: Caddy vor der Web-App, Migrationen als eigener Container,
alles hinter `https://localhost`.

```bash
cd deploy
cp .env.example .env              # einmalig: die drei DB-Passwörter eintragen, der Rest ist lokal optional
docker compose up -d --build      # baut die Images und startet alles
docker compose ps -a              # alle Dienste "healthy", migrate "Exited (0)" (-a zeigt auch beendete)
```

Dann https://localhost öffnen. Der Browser warnt vor dem Zertifikat, weil Caddy es lokal selbst ausstellt.
Einmal „Trotzdem fortfahren“ klicken (oder dem Zertifikat vertrauen, siehe unten). Alle Mails landen in Mailpit
unter http://localhost:8025.

| Dienst | Adresse | Was passiert |
|---|---|---|
| `caddy` | https://localhost (80 → 443) | Reverse Proxy mit HTTPS, einziger Eingang von außen |
| `web` | nur intern (`web:8080`) | die Website, Healthcheck mit `dotnet PubPot.Web.dll healthcheck` |
| `migrate` | – | spielt die EF-Migrationen ein und beendet sich, erst danach startet `web` |
| `db`, `mailpit`, `adminer` | wie oben | wie bei den Hilfsdiensten |
| `migrate-twitch`, `worker` | – | nur mit `--profile worker` (siehe [Worker-README](src/TwitchWorker/README.md)) |

Admin-Zugang im Container: Hash erzeugen (fragt das Passwort verdeckt ab), die ausgegebene Zeile in
`deploy/.env` eintragen und `web` neu starten:

```bash
docker compose run --rm --no-deps web hash-password
docker compose up -d web          # übernimmt die geänderte .env
```

Weitere Befehle:

```bash
docker compose logs -f web                     # Log der Website (eine JSON-Zeile pro Eintrag)
docker compose logs --no-log-prefix web | jq -r '"\(.Timestamp) \(.LogLevel) \(.Message)"'  # lesbar, mit jq
docker compose up -d --build web               # nach einer Code-Änderung neu bauen und starten
docker compose --profile worker up -d --build  # mit Twitch-Worker (Zugangsdaten in .env)
docker compose down                            # alles stoppen, Daten bleiben
```

Smoke-Test wie in der CI (startet den Stack ohne Override mit fertigen Images in einem eigenen Projekt, prüft ihn
und räumt wieder auf): `./scripts/smoke-test.sh`. Vorher die Images bauen (`docker compose build`).

**Monitoring** (Profil `monitoring`): erst `GRAFANA_ADMIN_PASSWORD` in `deploy/.env` setzen, dann

```bash
docker compose --profile monitoring up -d   # Prometheus, Grafana, node-exporter, cAdvisor
```

Grafana: http://localhost:3000 (Dashboard „PubPot – Start“), Prometheus: http://localhost:9090, Alertmanager:
http://localhost:9093. Alle sind nur auf diesem Rechner erreichbar. Alarm-Mails landen lokal in Mailpit
(http://localhost:8025). Die Konfiguration liegt in `deploy/monitoring/`.

**Backups** (Dienst `backup`, läuft immer mit): beim ersten Start sofort, danach jede Nacht um 03:00. Ablage in
`deploy/backups/` (nicht im Git), 7 Tage täglich und 4 Wochen wöchentlich.

```bash
docker compose exec backup sh backup.sh now            # sofort sichern
./scripts/restore.sh                                     # vorhandene Backups anzeigen
./scripts/restore.sh daily/pubpot-<Zeit>.dump            # Datenbank pubpot daraus wiederherstellen (fragt vorher)
```

**Restore von Hand prüfen**, ohne die echte Datenbank anzufassen. Der Smoke-Test macht dasselbe automatisch:

1. Frisches Backup: `docker compose exec backup sh backup.sh now`
2. Dateiname ansehen: `./scripts/restore.sh`
3. In eine Test-Datenbank einspielen: `./scripts/restore.sh daily/pubpot-<Zeit>.dump pubpot_restore_test`
4. Vergleichen, z. B. die Anzahl der Anmeldungen in beiden Datenbanken:
   `docker compose exec db psql -U postgres -d pubpot_restore_test -c "select count(*) from signups"` (und dasselbe mit
   `-d pubpot`). Oder in Adminer (http://localhost:8081, Server `db`) durch beide Datenbanken klicken.
5. Aufräumen: `docker compose exec db dropdb -U postgres pubpot_restore_test`

### Caddy-Zertifikat vertrauen (optional)

Damit die Warnung verschwindet, Caddys lokales Stammzertifikat für deinen Windows-Benutzer eintragen
(PowerShell, keine Admin-Rechte nötig). Chrome und Edge nutzen diesen Speicher, Firefox einen eigenen.

```powershell
cd deploy
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt $env:TEMP\caddy-root.crt
certutil -user -addstore Root $env:TEMP\caddy-root.crt
```

Wieder entfernen: `certutil -user -delstore Root "Caddy Local Authority - 2026 ECC Root"`. Das Zertifikat gilt nur
für dieses Caddy-Volume. Nach `docker compose down -v` erzeugt Caddy ein neues, dann das alte entfernen und neu eintragen.

### Umgebungsvariablen (`deploy/.env`)

Compose setzt die Werte aus `deploy/.env` in `compose.yaml` ein. Jeder Container bekommt nur seine eigenen (E-037).
Die Vorlage mit Kommentaren ist [`deploy/.env.example`](deploy/.env.example).

| Variable | Pflicht | Bedeutung |
|---|---|---|
| `POSTGRES_PASSWORD`, `PUBPOT_DB_PASSWORD`, `TWITCH_DB_PASSWORD` | ja | DB-Passwörter (Superuser, Web, Worker), nur Buchstaben und Ziffern |
| `SITE_DOMAIN` | Server | Domain für Caddy, Standard `localhost` |
| `BIND_ADDRESS` | Server | `0.0.0.0` auf dem Server, Standard `127.0.0.1` |
| `EDGE_SUBNET` | – | Subnetz zwischen Caddy und web, Standard `172.30.0.0/24`. Ein zweites Projekt auf demselben Rechner braucht ein eigenes |
| `Site__BaseUrl` | – | Adresse für Links in Mails, Standard `https://${SITE_DOMAIN}` |
| `Smtp__Host`, `Smtp__Port`, `Smtp__Security`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress`, `Smtp__FromName` | Server | Mailversand. Lokal Standard: Mailpit |
| `Admin__Username`, `Admin__PasswordHash` | – | Admin-Bereich, ohne Hash aus |
| `Notifications__Email`, `Notifications__DiscordWebhookUrl` | – | Hinweise auf neue Anmeldungen |
| `COMPOSE_PROFILES` | – | `worker` schaltet den Twitch-Worker dauerhaft ein |
| `Twitch__ClientId`, `Twitch__ClientSecret` | mit Worker | Twitch-App (geheim) |
| `Worker__Languages`, `Worker__IntervalMinutes`, `Worker__StopAfterHours`, `Worker__GameIds`, `Worker__UserLogins` | – | was der Worker sammelt |
| `IMAGE_TAG` | Server | Image-Version, lokal `local` |

`Proxy__KnownNetworks` folgt automatisch aus `EDGE_SUBNET`. Weitere Einstellungen wie Spam-Schutz,
Aufräum-Job und Mail-Ausgang haben Standardwerte in `appsettings.json`. Compose reicht sie bewusst nicht durch.

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
| `Admin:MaxFailedLogins` / `Admin:LockoutDuration` | `Admin__MaxFailedLogins` / `Admin__LockoutDuration` | Sperre nach Fehlversuchen, Standard 5 und `00:15:00` |
| `Admin:IdleTimeout` / `Admin:MaxSessionLifetime` | `Admin__IdleTimeout` / `Admin__MaxSessionLifetime` | Sitzungsdauer, Standard `08:00:00` und `1.00:00:00` |
| `Admin:LoginPostsPerWindow` / `Admin:LoginWindow` | `Admin__LoginPostsPerWindow` / `Admin__LoginWindow` | Rate-Limit pro IP, Standard 10 in `00:10:00` |

Danach ist der Admin-Bereich unter http://localhost:5080/admin erreichbar.

### Hinweise auf neue Anmeldungen (optional)

| Einstellung | Umgebungsvariable | Bedeutung |
|---|---|---|
| `Notifications:Email` | `Notifications__Email` | Bekommt bei jeder neu bestätigten Anmeldung eine kurze Mail (ohne Name und Adresse). Leer = keine Mail. |
| `Notifications:DiscordWebhookUrl` | `Notifications__DiscordWebhookUrl` | Discord-Webhook (`https://discord.com/api/webhooks/…`). **Geheim**, nur in `.env` oder User Secrets. Leer = Discord aus. |

> Ohne Startprofil muss die Umgebung über die Variable gesetzt werden: `ASPNETCORE_ENVIRONMENT=Development`.
> Nicht `dotnet run --environment Development` verwenden. Das setzt in .NET 10 eine Umgebungsvariable namens „Development“
> und startet die App im Production-Modus, ohne CSS (siehe `docs/ENTSCHEIDUNGEN.md`, E-016).
