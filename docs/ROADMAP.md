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

## Phase 2 – Web-Projekt mit allen Seiten, DE/EN, ohne Formulare ✅
| # | Schritt | Status |
|---|---|---|
| 2.1 | Razor-Pages-Projekt, Layout, Design-Grundlage (CSS-Variablen, Hell/Dunkel, mobil zuerst), selbst gehostete Schrift, Wortmarke | ✅ |
| 2.2 | Lokalisierung: Routen `/de` und `/en`, `.resx`, Sprachumschalter, Vorschlag nach Browsersprache, `hreflang` | ✅ |
| 2.3 | Restliche Seiten als Gerüst: Anmeldung (ohne Formular), Danke, Bestätigung, Impressum/Datenschutz (Platzhalter mit TODO), `robots.txt` | ✅ |
| 2.4 | Startseite: Hero, 3 Schritte, Rechenbeispiel, Entwickler, Creator, Pilot/Next Fest, FAQ, Buttons | ✅ |
| 2.5 | Security-Header und strikte CSP von Anfang an, `/health` | ✅ |
| 2.6 | Tests (`WebApplicationFactory`): Seiten, Links (Crawler), Sprachen, Header, keine externen Quellen, Übersetzungen, Kontraste | ✅ |

> 2.3 und 2.4 getauscht: Die Startseite verlinkt auf die Anmeldeseiten, die deshalb zuerst existieren müssen.
> Ergebnis Lighthouse (Release-Build, `/de`): mobil 99/100/100/100, Desktop 100/100/100/100.

## Phase 3 – Formulare, Datenbank, Double-Opt-in, Spam-Schutz 🔄
| # | Schritt | Status |
|---|---|---|
| 3.1 | Docker für die lokalen Hilfsdienste: `deploy/compose.yaml` (Postgres 16, Init-Skript mit zwei Datenbanken und eigenen Benutzern, E-011), `deploy/compose.override.yaml` (Mailpit, Adminer, nur `127.0.0.1`), `deploy/.env.example` | ✅ |
| 3.2 | EF Core: `AppDbContext`, Datenmodell (Anmeldungen, Bestätigungen, Mail-Ausgang), snake_case-Namen, erste Migration, `dotnet-ef` als lokales Tool | ⬜ |
| 3.3 | `/health/ready` mit Datenbank-Prüfung, Test-Basis mit Testcontainers (echtes Postgres im Test) | ⬜ |
| 3.4 | Formular Entwickler: Felder, serverseitige Validierung (Steam-Link), Einwilligung mit Version und Zeitstempel, Speichern, Zusammenführen bei gleicher E-Mail | ⬜ |
| 3.5 | Formular Creator: Plattformen (Mehrfachauswahl), Kanal/Twitch-Login, Zuschauer-Bereich, Genres, 18+-Bestätigung | ⬜ |
| 3.6 | Double-Opt-in: Token (nur Hash gespeichert), Mail-Ausgang in der Datenbank + Versand per SMTP im Hintergrund, Bestätigungsseite (Klick auf Button statt reinem Link-Aufruf), Test mit Mailpit | ⬜ |
| 3.7 | Spam-Schutz: Honeypot, Mindestausfüllzeit, Rate-Limiting pro IP, Antiforgery | ⬜ |
| 3.8 | Aufräum-Job: unbestätigte Anmeldungen nach 30 Tagen löschen, versendete Mails nach 7 Tagen | ⬜ |
| 3.9 | Abnahme: kompletter Ablauf im Browser mit Mailpit, Lighthouse auf den Formularen, Datenschutz-Fakten aktualisieren | ⬜ |

## Phase 4 – Admin-Bereich ⬜
Grob: Login mit Hash aus Umgebungsvariable + Sperre · Hilfsbefehl zum Erzeugen des Hashes · Liste mit Filter und Suche · Pipeline-Status, Notiz, nächster Schritt · CSV-Export · Löschen (DSGVO) · Benachrichtigung per E-Mail / Discord.

## Phase 5 – Docker und Compose lokal ⬜
Grob: Multi-Stage-Dockerfiles (Nicht-Root, Healthcheck) · Migrations-Bundle als einmaliger Container · `compose.yaml` (web, worker, db, caddy, migrate) · `compose.override.yaml` (mailpit, adminer an 127.0.0.1) · DB-Init mit zwei Datenbanken und eigenen Benutzern (E-011) · Worker-Schema als SQL-Skript · `.env.example` · Abnahme: `docker compose up` + Double-Opt-in mit Mailpit.

## Phase 6 – CI ⬜
Grob: PR-Workflow (restore, build, format, Tests mit Postgres, Images bauen) · Push nach GHCR mit SHA-Tag bei Merge auf `main` · Dependabot (NuGet, Docker, Actions; keine 11.x-Pakete, E-007) · Secret Scanning.

> **Phasen 7 und 8 sind gegenüber dem Auftrag getauscht** (E-013): Erst läuft alles lokal, dann kommt der VPS.

## Phase 7 – Monitoring, Alarme, Backups (lokal) ⬜
Grob: JSON-Logs ohne personenbezogene Daten · OpenTelemetry + `/metrics` · Prometheus, Grafana mit Start-Dashboard, node-exporter, cAdvisor · Uptime Kuma + Push-URL im Worker · Alarme per E-Mail, lokal an Mailpit (E-014) · nächtliche Backups mit Rotation in einen lokalen Ordner, Restore-Skript, dokumentierter Restore-Test. Alle Oberflächen nur an `127.0.0.1`.

## Phase 8 – VPS und CD: Staging, dann Production ⬜
Grob: vorher F-008 klären (VPS, Backup-Ziel, Domain, SMTP) · Anleitung VPS-Härtung · Compose-Konfiguration für staging/production · Deploy-Skript (pull, migrate, up, Smoke-Test, automatischer Rollback) · Staging bei Merge auf `main` · Production über Tag `v*` mit manueller Freigabe · Monitoring auf dem VPS, Zugang per SSH-Tunnel (F-009) · Backups auf externen Speicher · README fertig.
