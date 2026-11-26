# Roadmap PubPot V1

Hier steht, **was** gebaut wird und **wie weit** es ist. Das ist die einzige Quelle für den Status.
Den Verlauf (was wann passiert ist) findest du in [JOURNAL.md](JOURNAL.md), offene Fragen in [RUECKFRAGEN.md](RUECKFRAGEN.md).

## Status-Legende

| Symbol | Bedeutung |
|---|---|
| ⬜ | geplant |
| 🔄 | in Arbeit |
| ⏸️ | blockiert (wartet auf eine Rückfrage, z. B. `F-003`) |
| ✅ | fertig (committet, Build grün) |
| ❌ | verworfen (mit Begründung) |

**Regeln**
- Jeder Schritt `x.y` ergibt **einen lauffähigen Commit**. Die Commit-Nachricht beginnt mit der Schrittnummer, z. B. `1.3: Worker auf .NET 10 gehoben`.
- Ich plane schrittweise: Die aktuelle Phase ist fein aufgeteilt, spätere Phasen nur grob. Bevor eine Phase beginnt, verfeinere ich sie hier.
- Bevor ich vom Auftrag abweiche oder ein Dienst Geld kostet, stelle ich die Frage in `RUECKFRAGEN.md` und warte auf deine Antwort.

---

## Phase 0 – Arbeitsweise und Repo ✅
| # | Schritt | Status |
|---|---|---|
| 0.1 | Git-Repo, Plan-Dateien, Rückfragen, Journal, Entscheidungslog, Checkliste | ✅ |

## Phase 1 – Repo-Struktur und Worker ✅
| # | Schritt | Status |
|---|---|---|
| 1.1 | Worker **unverändert** nach `src/TwitchWorker` kopieren, ohne Secrets (bereinigte `.env.example`). Eigener Commit, damit der Umbau danach im Diff sichtbar ist | ✅ |
| 1.2 | Grundgerüst: `PubPot.slnx`, `global.json` (SDK 11), `Directory.Build.props` (gemeinsame Einstellungen, Analyzer), `Directory.Packages.props` (zentrale Paketversionen), `.editorconfig`, `.gitattributes`. Worker eingebunden, noch auf .NET 8 | ✅ |
| 1.3 | Worker auf .NET 10 und Npgsql 10 heben, `dotnet format`, Build grün, kurzer Lauf lokal gegen eine Test-DB | ✅ |
| 1.4 | Leeres Testprojekt `tests/PubPot.Web.Tests` (xUnit v3, Microsoft Testing Platform) als Platzhalter, `dotnet test` läuft | ✅ |

> Reihenfolge von 1.1 und 1.2 getauscht: Mit zentraler Paketverwaltung wäre die unveränderte `.csproj` nicht baubar, und jeder Commit soll lauffähig sein.

## Phase 2 – Web-Projekt mit allen Seiten, DE/EN, ohne Formulare ⬜
| # | Schritt | Status |
|---|---|---|
| 2.1 | Razor-Pages-Projekt, Layout, Design-Grundlage (CSS-Variablen, Hell/Dunkel, mobil zuerst), selbst gehostete Schrift, Wortmarke | ⬜ |
| 2.2 | Lokalisierung: Routen `/de` und `/en`, `.resx`, Sprachumschalter, Vorschlag nach Browsersprache, `hreflang` | ⬜ |
| 2.3 | Startseite: Hero, 3 Schritte, Rechenbeispiel, Entwickler, Creator, Pilot/Next Fest, FAQ, Buttons | ⬜ |
| 2.4 | Restliche Seiten als Gerüst: Anmeldung (ohne Formular), Danke, Bestätigung, Impressum/Datenschutz (Platzhalter mit TODO) | ⬜ |
| 2.5 | Security-Header und strikte CSP von Anfang an, `/health` | ⬜ |
| 2.6 | Erste Tests (`WebApplicationFactory`): Seiten liefern 200, Header vorhanden, hreflang korrekt | ⬜ |

## Phase 3 – Formulare, Datenbank, Double-Opt-in, Spam-Schutz ⬜
Grob: EF Core + erste Migration · Formular Entwickler · Formular Creator · Einwilligung mit Version/Zeitstempel · Zusammenführen bei gleicher E-Mail · Double-Opt-in mit gehashtem Token · SMTP (lokal Mailpit) · Honeypot, Mindestzeit, Rate-Limit, Antiforgery · Löschjob für Unbestätigte > 30 Tage · `/health/ready` · Integrationstests mit Testcontainers.

## Phase 4 – Admin-Bereich ⬜
Grob: Login mit Hash aus Umgebungsvariable + Sperre · Hilfsbefehl zum Erzeugen des Hashes · Liste mit Filter und Suche · Pipeline-Status, Notiz, nächster Schritt · CSV-Export · Löschen (DSGVO) · Benachrichtigung per E-Mail / Discord.

## Phase 5 – Docker und Compose lokal ⬜
Grob: Multi-Stage-Dockerfiles (Nicht-Root, Healthcheck) · Migrations-Bundle als einmaliger Container · `compose.yaml` (web, worker, db, caddy, migrate) · `compose.override.yaml` (mailpit, adminer an 127.0.0.1) · DB-Init mit zwei Datenbanken und eigenen Benutzern · `.env.example` · Abnahme: `docker compose up` + Double-Opt-in mit Mailpit.

## Phase 6 – CI ⬜
Grob: PR-Workflow (restore, build, format, Tests mit Postgres, Images bauen) · Dependabot (NuGet, Docker, Actions).

## Phase 7 – CD: Staging, dann Production ⬜
Grob: Anleitung VPS-Härtung · Compose-Konfiguration für staging/production · Push nach GHCR mit SHA-Tag · Deploy-Skript (pull, migrate, up, Smoke-Test, automatischer Rollback) · Staging bei Merge auf `main` · Production über Tag `v*` mit manueller Freigabe.

## Phase 8 – Monitoring, Alarme, Backups ⬜
Grob: JSON-Logs ohne personenbezogene Daten · OpenTelemetry + `/metrics` · Prometheus, Grafana mit Start-Dashboard, node-exporter, cAdvisor · Uptime Kuma + Push-URL im Worker · Alarme · nächtliche Backups mit Rotation, Restore-Skript, dokumentierter Restore-Test · README fertig.
