# Roadmap PubPot V1

Hier steht, **was** gebaut wird und **wie weit** es ist. Das ist die einzige Quelle für den Status.
Den Verlauf (was wann passiert ist) findest du in [JOURNAL.md](JOURNAL.md), offene Fragen in [RUECKFRAGEN.md](RUECKFRAGEN.md).
Ideen für die Plattform nach V1 (noch nicht geplant) sammelt [IDEEN.md](IDEEN.md).

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

## Phase 3 – Formulare, Datenbank, Double-Opt-in, Spam-Schutz ✅
| # | Schritt | Status |
|---|---|---|
| 3.1 | Docker für die lokalen Hilfsdienste: `deploy/compose.yaml` (Postgres 16, Init-Skript mit zwei Datenbanken und eigenen Benutzern, E-011), `deploy/compose.override.yaml` (Mailpit, Adminer, nur `127.0.0.1`), `deploy/.env.example` | ✅ |
| 3.2 | EF Core: `AppDbContext`, Datenmodell (Anmeldungen, Bestätigungen, Mail-Ausgang), snake_case-Namen, erste Migration, `dotnet-ef` als lokales Tool | ✅ |
| 3.3 | `/health/ready` mit Datenbank-Prüfung, Test-Basis mit Testcontainers (echtes Postgres im Test) | ✅ |
| 3.4 | Formular Entwickler: Felder, serverseitige Validierung (Steam-Link), Einwilligung mit Version und Zeitstempel, Speichern, Zusammenführen bei gleicher E-Mail | ✅ |
| 3.5 | Formular Creator: Plattformen (Mehrfachauswahl), Kanal/Twitch-Login, Zuschauer-Bereich, Genres, 18+-Bestätigung | ✅ |
| 3.6 | Double-Opt-in: Token (nur Hash gespeichert), Mail-Ausgang in der Datenbank + Versand per SMTP im Hintergrund, Bestätigungsseite (Klick auf Button statt reinem Link-Aufruf), Test mit Mailpit | ✅ |
| 3.7 | Spam-Schutz: Honeypot, Mindestausfüllzeit, Rate-Limiting pro IP, Antiforgery | ✅ |
| 3.8 | Aufräum-Job: unbestätigte Anmeldungen nach 30 Tagen löschen, versendete Mails nach 7 Tagen | ✅ |
| 3.9 | Abnahme: kompletter Ablauf im Browser mit Mailpit, Lighthouse auf den Formularen, Datenschutz-Fakten aktualisieren | ✅ |

## Phase 4 – Admin-Bereich ✅
| # | Schritt | Status |
|---|---|---|
| 4.1 | **Admin-Konfiguration und Hash-Befehl:** `AdminOptions` (`Admin__Username`, `Admin__PasswordHash`). Ohne Hash ist der Admin-Bereich aus (404) und es gibt eine Warnung im Log. Befehl `dotnet run --project src/PubPot.Web -- hash-password` liest das Passwort verdeckt ein und gibt den Hash aus. Hashing mit `PasswordHasher` aus ASP.NET Core Identity (PBKDF2, E-025) | ✅ |
| 4.2 | **Login, Logout, Sperre:** eigenes Cookie-Schema nur für `/admin` (`__Host-`-Cookie, HttpOnly, Secure, SameSite=Strict, 8 h gleitend). Login-Seite, Logout per POST. Sperre nach 5 Fehlversuchen für 15 Minuten, dazu Rate-Limit auf den Login-POST pro IP. Admin-Layout, `X-Robots-Tag: noindex`, leere Startseite `/admin` | ✅ |
| 4.3 | **Datenmodell Pipeline:** neue Spalten `pipeline_status` (neu, kontaktiert, Gespräch, Pilot, abgesagt), `admin_note`, `next_step_on`, `admin_updated_at`. Migration 4, bestehende Einträge bekommen „neu“ | ✅ |
| 4.4 | **Liste:** alle Anmeldungen als Tabelle, Filter (Entwickler/Creator, bestätigt, Status, Sprache), Suche in Name, E-Mail, Studio, Spiel und Kanal, 50 pro Seite. Die Suche geht per POST, damit keine E-Mail-Adressen in URLs und Logs landen. Filter ohne Personenbezug stehen in der URL (E-027) | ✅ |
| 4.5 | **Detailseite:** alle Angaben einer Anmeldung, Pipeline-Status, Notiz und Datum des nächsten Schritts bearbeiten. In der Adresse steht nur die ID | ✅ |
| 4.6 | **Löschen (DSGVO):** Rückfrage „Wirklich löschen?“, dann wird die Anmeldung samt Bestätigungen und noch offener Mails an diese Adresse gelöscht. Ins Log kommt nur die ID | ✅ |
| 4.7 | **CSV-Export** der aktuell gefilterten Liste: UTF-8 mit BOM und Semikolon (öffnet direkt in Excel), Schutz vor CSV-Injection (Zellen mit `=`, `+`, `-`, `@` werden entschärft, E-029) | ✅ |
| 4.8 | **Benachrichtigung** bei neuer bestätigter Anmeldung: Mail über die Outbox an `Notifications__Email` (E-014, E-030). Discord-Webhook nur, wenn `Notifications__DiscordWebhookUrl` gesetzt ist. Beide Nachrichten ohne persönliche Daten, nur Art der Anmeldung und Link zur Detailseite | ✅ |
| 4.9 | **Aufbewahrungsfrist (E-023):** Spalten für letzte Aktivität und Warnmail, täglicher Job: Warnmail nach 12 Monaten, Seite „Ich möchte dabei bleiben“ mit Button-POST, Mail „Löschung verhindert“, Löschen 30 Tage nach der Warnmail. Datenschutzerklärung ergänzen (anwaltlich prüfen lassen) | ✅ |
| 4.10 | **Abnahme:** kompletter Ablauf im Browser (Login, Sperre, Filter, Bearbeiten, Export, Löschen, Benachrichtigung in Mailpit), Lighthouse auf den Admin-Seiten, README um alle neuen Umgebungsvariablen ergänzen | ✅ |

**Was für alle Schritte gilt**
- Jeder Schritt bringt eigene Tests mit (Integrationstests mit Testcontainers).
- Der Admin-Bereich hält sich an dieselbe strikte CSP: kein Inline-Style, kein Inline-Skript.
- Jede Admin-Seite verlangt den Login. Ein Test prüft das für alle Seiten auf einmal, damit keine neue Seite ungeschützt bleibt.
- Die Sperre zählt im Arbeitsspeicher. Nach einem Neustart beginnt die Zählung neu. Für einen Container reicht das.
- Der Admin-Bereich ist nur auf Deutsch, ohne `/de`- oder `/en`-Route (F-014, E-024).
- Für die Aufbewahrungsfrist zählt nur eine **Änderung** im Admin-Bereich als Aktivität, nicht der Status an sich (F-015, E-023).

> Ergebnis Lighthouse (Abnahme, lokal): Login, Liste, Detail, Löschen und `/de|en/signup/keep` jeweils mobil und Desktop **100/100/100** (Performance, Barrierefreiheit, Best Practices). SEO 54 nur wegen des gewollten `noindex` (`is-crawlable`) und der dann überflüssigen Meta-Beschreibung, genauso wie bei der Bestätigungsseite.

## Phase 5 – Docker und Compose lokal ✅
Ziel: `docker compose up` startet die ganze Anwendung lokal, so wie sie später auf dem VPS läuft. Die Anmeldung samt Double-Opt-in lässt sich unter `https://localhost` mit Mailpit testen.

| # | Schritt | Status |
|---|---|---|
| 5.1 | **Forwarded Headers:** Hinter Caddy liest die App `X-Forwarded-For` (echte Client-IP fürs Rate-Limiting) und `X-Forwarded-Proto` (HTTPS für HSTS und Secure-Cookies). Vertraut wird nur einem festen Docker-Netz (`Proxy__KnownNetworks`), sonst könnte jeder Besucher seine IP fälschen. Tests für vertrauten und nicht vertrauten Absender (E-018, E-022) | ✅ |
| 5.2 | **Dockerfile Web:** Multi-Stage (Build mit SDK 11, Laufzeit `aspnet:10.0` in der „chiseled“-Variante: ohne Shell und Paketmanager, Nicht-Root). Healthcheck als eingebauter Befehl `healthcheck`, weil das Image weder `curl` noch `wget` enthält. Restore als eigene Schicht für schnelle Builds | ✅ |
| 5.3 | **Migrations-Bundle:** zweites Ziel `migrate` im selben Dockerfile mit `dotnet ef migrations bundle`. Es läuft als einmaliger Container vor dem Start der App und braucht nur den Connection-String. Probelauf gegen die lokale Datenbank (zweimal hintereinander, der zweite Lauf ändert nichts) | ✅ |
| 5.4 | **Worker:** Schema als versionierte SQL-Skripte (`deploy/db/twitch/001_schema.sql`) mit kleinem Runner und Tabelle `schema_migrations`, `CREATE TABLE` fliegt aus dem Code (E-011). Der Worker prüft beim Start nur noch, ob das Schema da ist. Dockerfile auf chiseled umgestellt, Healthcheck (Alter der Heartbeat-Datei) als eingebauter Befehl. Die alte `src/TwitchWorker/docker-compose.yml` entfällt | ✅ |
| 5.5 | **Compose für die Anwendung:** Dienste `migrate`, `web`, `migrate-twitch`, `worker` in `compose.yaml`. Startreihenfolge über `depends_on` (db gesund → Migration erfolgreich beendet → web). Jeder Dienst bekommt nur die Variablen, die er braucht. Härtung: schreibgeschütztes Dateisystem, `/tmp` als tmpfs, `no-new-privileges`, alle Linux-Capabilities entzogen. `build:` steht nur in `compose.override.yaml`, auf dem Server werden fertige Images gezogen | ✅ |
| 5.6 | **Caddy:** `deploy/caddy/Caddyfile` mit Domain aus `SITE_DOMAIN`. Lokal `localhost` mit Caddys eigener Zertifizierungsstelle, auf dem VPS automatisch Let's Encrypt. Zwei Netze: `edge` (Caddy, web) und `backend` (web, db, worker …), Caddy kommt nie an die Datenbank. Ports über `BIND_ADDRESS` (Standard `127.0.0.1`). Kein Access-Log, weil IP-Adressen personenbezogen sind. `Server`-Header entfernt | ✅ |
| 5.7 | **`.env.example` und Doku:** alle Variablen mit Kommentar (auch `Admin__PasswordHash`, `Notifications__Email`, `Notifications__DiscordWebhookUrl`, `Site__BaseUrl`), Befehl `docker compose run --rm --no-deps web hash-password`. README-Abschnitt „Alles in Docker starten“ und optional „Caddy-Zertifikat vertrauen“. Checkliste ergänzen | ✅ |
| 5.8 | **Abnahme:** Start von null in einem eigenen Compose-Projekt (deine Entwicklungsdaten bleiben unberührt): alle Container gesund, Migration mit Exit-Code 0, Double-Opt-in im Browser über `https://localhost` mit Mailpit, Admin-Login über HTTPS (Secure-Cookie), HSTS-Header da, Container laufen nicht als root. Lighthouse über Caddy, Größe der Images notieren | ✅ |

**Was für alle Schritte gilt**
- Die bisherigen Wege bleiben: `dotnet run` gegen die Datenbank aus `deploy/` und `./scripts/check.sh` funktionieren weiter.
- Eine `.env` für alle, aber Compose reicht jedem Container nur seine eigenen Werte weiter (`environment:`, kein `env_file:` mit allem). Die Web-App sieht also nie das Twitch-Secret.
- Images heißen schon so wie später in der GitHub Container Registry (`ghcr.io/tidragounity/pubpot-web`, `…-worker`, `…-migrate`), mit Tag `${IMAGE_TAG:-local}`. Phase 6 baut und pusht sie, Phase 9 zieht sie auf dem VPS.
- Getrennte Konfiguration für staging und production kommt in Phase 9. `compose.yaml` ist dafür schon so gebaut, dass nur die `.env` wechselt.
- `C:\Timo\Docker	witch-worker` und seine Container bleiben unberührt (F-001), siehe F-016.

> Ergebnis der Abnahme: von leerem Volume bis „alles gesund“ in 46 s. Lighthouse über Caddy (`https://localhost/de` und `/de/signup/creator`, mobil und Desktop) **100/100/100/100**. Images entpackt: web 261 MB, migrate 295 MB, worker 146 MB, Caddy 92 MB. HSTS kommt für `localhost` absichtlich nicht (E-038), mit echter Domain prüfen das die Tests aus 5.1.

## Phase 6 – CI ✅
Ziel: Jeder Pull Request wird automatisch gebaut, getestet und als Docker-Stack gestartet. Nach dem Merge auf `main` liegen fertige Images mit Commit-SHA in der GitHub Container Registry, bereit für den Deploy in Phase 9.

**Vorab von dir:** Dein GitHub-Token braucht zusätzlich die Rechte **Workflows** (Lesen und Schreiben, sonst lehnt GitHub den Push von `.github/workflows` ab), **Pull requests** (Lesen und Schreiben) und für 6.7 **Administration** (Lesen und Schreiben). Anleitung in der Checkliste.

| # | Schritt | Status |
|---|---|---|
| 6.1 | **CI-Workflow Build und Tests** (`.github/workflows/ci.yml`): bei jedem Pull Request und Push auf `main`. SDK 11 aus `global.json` plus .NET-10-Laufzeit, NuGet-Cache, dann `./scripts/check.sh`, also genau dieselben Prüfungen wie lokal (Build mit Warnungen als Fehler, Format, beide Testprojekte mit Testcontainers). Testergebnisse als Artefakt, wenn etwas fehlschlägt. Minimale Rechte (`contents: read`), Actions auf Commit-SHA festgenagelt, alte Läufe desselben PRs werden abgebrochen | ✅ |
| 6.2 | **Images bauen und Compose-Smoke-Test:** die drei Images (web, migrate, worker) mit Buildx und Build-Cache bauen, in PRs ohne Push. Danach `docker compose up --wait` mit Wegwerf-Passwörtern: migrate muss mit 0 enden, `https://localhost/health/ready` und `/de` müssen 200 liefern, `migrate-twitch` läuft auch. Bei Fehlern landen die Container-Logs im Lauf | ✅ |
| 6.3 | **Push nach GHCR** nur bei Merge auf `main` und nur, wenn 6.1 und 6.2 grün sind: Tags `<commit-sha>` (unveränderlich, für den Deploy) und `main` (zeigt immer auf den neuesten Stand). OCI-Labels (Quelle, Commit), SBOM und Herkunftsnachweis (Provenance) am Image. Schreibrecht für Packages nur in diesem Job | ✅ |
| 6.4 | **Image-Scan mit Trivy:** sucht in den drei Images nach bekannten Sicherheitslücken. Ergebnis als Tabelle in der Zusammenfassung des Laufs, rot nur bei **kritischen** Lücken, für die es schon ein Update gibt | ✅ |
| 6.5 | **Dependabot** (`.github/dependabot.yml`): NuGet, Dockerfiles, Compose-Dateien und GitHub Actions, wöchentlich, Updates gebündelt. .NET-, EF-Core- und Npgsql-Pakete sowie die Laufzeit-Images bleiben auf 10.x (E-007), Postgres auf 16. Dazu „Dependabot security updates“ einschalten | ✅ |
| 6.6 | **CodeQL** (Code-Scanning für C#, für öffentliche Repos kostenlos): findet typische Sicherheitsfehler im Code, bei PRs und wöchentlich. Funde erscheinen im Reiter „Security“ | ✅ |
| 6.7 | **Regeln für `main`** (Ruleset): Änderungen nur per Pull Request, die Prüfungen aus 6.1, 6.2 und 6.6 müssen grün sein, kein Force-Push, kein Löschen. Ab hier arbeiten wir mit Pull Requests (siehe F-019). README: CI-Badge und Abschnitt „CI“ | ✅ |
| 6.8 | **Abnahme:** Test-PR mit absichtlichem Formatfehler → rot und Merge gesperrt. Korrigiert → grün → Merge → Images mit SHA in GHCR. Lokal die Images aus GHCR ziehen und starten (`IMAGE_TAG=<sha>`, ohne Build). Erste Dependabot-PRs laufen durch die CI | ✅ |

**Was für alle Schritte gilt**
- **Kosten:** keine. GitHub Actions, GHCR, Dependabot, Secret Scanning und CodeQL sind für öffentliche Repos kostenlos.
- **Lokal = CI:** Die CI ruft dieselben Befehle auf wie du (`check.sh`, `docker compose`). Was lokal grün ist, ist es auch in der CI und umgekehrt.
- **Keine Secrets in der CI:** Phase 6 braucht nur das automatische `GITHUB_TOKEN`. Die Passwörter für den Smoke-Test entstehen im Lauf und verschwinden mit ihm. Echte Secrets (SSH, Server) kommen erst in Phase 9.
- **Lieferkette:** Fremde Actions sind auf einen Commit-SHA festgenagelt, nicht auf ein Tag wie `v4`, das der Autor jederzeit umbiegen kann. Dependabot hält die SHAs aktuell.
- Secret Scanning und Push Protection sind im Repo schon an (geprüft).

> Ergebnis der Abnahme: Direkter Push auf `main` wird abgelehnt („repository rule violations“). Ein PR mit absichtlichem Formatfehler wird rot, Merge-Status `BLOCKED`, Merge-Versuch abgewiesen. Nach der Korrektur grün und gemergt. Die Images liegen öffentlich in GHCR (SHA und `main`). Der erste Dependabot-PR (MailKit 5.0) wurde von der CI gestoppt, weil sich die API geändert hat. CI-Laufzeit pro PR: etwa 2 Minuten (Tests) bzw. 2 bis 3 Minuten (Images, Smoke-Test, Trivy), parallel.

> **Phasen 7 und 8 sind gegenüber dem Auftrag getauscht** (E-013): Erst läuft alles lokal, dann kommt der VPS.

## Phase 7 – Monitoring, Alarme, Backups (lokal) ✅
Ziel: Du siehst in Grafana, wie es der Anwendung geht, bekommst eine Mail, wenn etwas kaputt ist, und jede Nacht entsteht ein Backup, dessen Wiederherstellung getestet ist. Alles läuft erst lokal (E-013), Alarme gehen an Mailpit. Auf dem VPS (Phase 9) läuft danach dasselbe.

| # | Schritt | Status |
|---|---|---|
| 7.1 | **JSON-Logs ohne personenbezogene Daten:** Web und Worker schreiben im Container eine JSON-Zeile pro Eintrag (Zeit in UTC, Kategorie, Level, Nachricht, Felder). Ein Test spielt Anmeldung, Bestätigung, Admin-Login und Löschen durch und prüft, dass weder E-Mail-Adresse noch Name, IP oder Token im Log landen | ✅ |
| 7.2 | **Metriken der Website** mit OpenTelemetry: Anfragen, Antwortzeiten, Fehlerquote (ASP.NET Core), .NET-Laufzeit (Speicher, GC, Threads), ausgehende HTTP-Aufrufe, dazu eigene Zähler: Anmeldungen und Bestätigungen (nach Art), verschickte und fehlgeschlagene Mails, gelöschte Einträge des Aufräum-Jobs. Endpunkt `/metrics` auf einem **eigenen internen Port** (9464), den Caddy nicht weiterleitet: Von außen ist er nicht erreichbar (Test) | ✅ |
| 7.3 | **Metriken des Workers und Push-URL:** Runden (vollständig/unvollständig), Streams pro Runde, Zeitpunkt der letzten vollständigen Runde. Optional `Worker__PushUrl`: wird nur nach einer **vollständigen** Runde aufgerufen (Punkt aus E-036). Damit lässt sich später ein externer Dienst anbinden | ✅ |
| 7.4 | **Prometheus, Grafana, node-exporter, cAdvisor** im Compose-Profil `monitoring`, Oberflächen nur auf `127.0.0.1`. Grafana startet mit fertiger Datenquelle und einem **Start-Dashboard** (Anfragen, Fehlerquote, Antwortzeiten, Anmeldungen pro Tag, Worker-Runden, CPU/RAM pro Container, freier Speicherplatz), alles als Dateien im Repo (`deploy/monitoring/`) | ✅ |
| 7.5 | **Erreichbarkeit mit dem Blackbox-Exporter** (F-021): Prometheus prüft die Website von außen über Caddy (Statuscode, HTTPS, Ablauf des Zertifikats, `/health/ready`). Den Worker überwacht Prometheus über seine Metriken aus 7.3 | ✅ |
| 7.6 | **Alarme per E-Mail** über Alertmanager (lokal an Mailpit, E-014): Website nicht erreichbar, Worker seit 15 Minuten ohne vollständige Runde, Fehlerquote über 5 % für 5 Minuten, Festplatte über 80 %, Backup fehlgeschlagen oder älter als 26 Stunden. Die Regeln haben **eigene Unit-Tests** (`promtool test rules`), die in der CI laufen | ✅ |
| 7.7 | **Backups:** Dienst `backup` sichert jede Nacht um 03:00 beide Datenbanken (`pg_dump`, dazu Rollen). Aufbewahrung 7 Tage täglich und 4 Wochen wöchentlich, Ordner per `BACKUP_DIR` (Standard `deploy/backups`, steht in `.gitignore`). Meldet Erfolg und Größe als Metrik. `scripts/restore.sh` stellt eine Datenbank aus einer Datei wieder her | ✅ |
| 7.8 | **Restore-Test:** automatisch im Smoke-Test der CI (Backup → in leere Datenbank zurückspielen → Anmeldungen und Worker-Daten vergleichen), dazu eine Anleitung für den Test von Hand in der README. Datenschutzerklärung: wie lange Daten in Backups bleiben (TODO, anwaltlich prüfen lassen) | ✅ |
| 7.9 | **Abnahme:** alles starten, Dashboard ansehen, **jeden Alarm einmal auslösen** (Website stoppen, Worker ohne Twitch, Fehler erzeugen, Backup kaputt machen) und die Mails in Mailpit prüfen. Restore von Hand. README-Abschnitt „Monitoring“ mit Zugängen, Ports und Befehlen | ✅ |
| 7.10 | **Nachbesserung, Zähler ab dem Start mit 0** (vom Nutzer gefunden, E-059): Die erste Anmeldung jeder Art fehlte im Dashboard, weil `increase()` den Schritt von „gab es nicht“ auf 1 nicht sieht. Alle eigenen Zähler (Website und Worker) melden sich beim Start mit 0. „Anmeldungen pro Tag“ zeigt echte Tagesbalken der letzten 7 Tage mit ganzen Zahlen | ✅ |

**Was für alle Schritte gilt**
- Jeder Schritt ist ein eigener Branch und PR (F-019). Neue Images (Prometheus, Grafana usw.) sind fest auf eine Version gepinnt, Dependabot hält sie aktuell.
- Die Monitoring-Dienste starten nur mit dem Profil `monitoring` (wie der Worker). Lokal: `docker compose --profile monitoring up -d`.
- Keine personenbezogenen Daten in Metriken: Labels nur mit festen Werten (Art der Anmeldung, Statuscode), nie mit E-Mail, IP oder Pfad mit ID.
- **Gut zu wissen für Phase 9 (VPS):** Monitoring auf demselben Server kann nicht melden, dass der Server selbst ausgefallen ist. Dafür braucht es später zusätzlich einen kostenlosen externen Dienst. Die Push-URL aus 7.3 ist dafür schon vorbereitet (healthchecks.io, E-060).

## Phase 8 – Twitch-Login, Spiele von Steam und Creator-Übersicht ⬜
Ziel: Creator melden sich mit ihrem Twitch-Konto an. Der Kanal ist damit **nachweislich ihrer**. Sie verwalten ihre Daten selbst und sehen, für welche Spiele sie wie viele Zuschauer gebracht haben. Im Admin-Bereich lädst du Spiele direkt von Steam (z. B. CS2, Dota 2) und siehst einen ersten Spielekatalog. Er ist die Vorlage dafür, wie du die Spieleseiten später gestalten willst (I-04). Alles läuft lokal, der VPS folgt in Phase 9.

**Bewusst über V1 hinaus (E-061):** Der Auftrag sieht für V1 nur die Landingpage vor. Login und Katalog sind ein erster Schritt Richtung Plattform. Das hast du so entschieden (27.11.).

**So funktioniert es (F-029, F-030):**
- **Twitch-Login über OpenID Connect,** den Standard für „Anmelden mit …“, mit dem offiziellen Paket von Microsoft. Twitch bestätigt uns die Twitch-ID, den Kanalnamen und die (bei Twitch bestätigte) E-Mail-Adresse.
  - Wir speichern nur die Twitch-ID und den Kanalnamen, **kein Zugangs-Token**.
  - Dafür gibt es eine **eigene Twitch-App** nur für den Login. Die Zugangsdaten deiner Studie bleiben unberührt (F-001).
- **Der Login ist freiwillig:** YouTuber und alle ohne Twitch melden sich weiter mit dem Formular an. Entwickler bekommen vorerst keinen Login.
- **Zuschauer pro Spiel aus den Worker-Daten:** Der Worker speichert für jeden Stream, den er sieht, Twitch-ID, Spiel und Zuschauerzahl. Die Web-App liest diese Daten über einen eigenen Datenbank-Benutzer, **nur lesend** und nur die zwei nötigen Tabellen. Die Trennung der Datenbanken (E-011) bleibt sonst bestehen.
- **Verkäufe und Einnahmen gibt es noch nicht** (keine Kampagnen, keine Verkaufszahlen). Sie erscheinen als **Beispielrechnung** in einer Vorschau im Admin-Bereich. Echte Creator sehen nur echte Zahlen.
- **Steam-Daten** kommen aus der öffentlichen Store-Schnittstelle von Steam (`appdetails`, ohne API-Schlüssel), auf Deutsch und Englisch.
  - Titelbild und Screenshots **kopieren wir auf den eigenen Server.** Sonst lüde jeder Besucher Bilder direkt bei Valve und gäbe dabei seine IP-Adresse weiter.
  - So bleibt auch die CSP streng (`img-src 'self'`).

| # | Schritt | Status |
|---|---|---|
| 8.1 | **Twitch-Login, die Technik:** OpenID Connect mit Twitch (`/signin-twitch`), eigenes Cookie für Creator (getrennt vom Admin-Login), Abmelden. Zugangsdaten per `TwitchLogin__ClientId` und `TwitchLogin__ClientSecret`, ohne sie ist der Knopf ausgeblendet. Twitch erlaubt nur `https`-Rücksprungadressen: lokal über Caddy (`https://localhost`) oder `dotnet run` zusätzlich auf `https://localhost:5081` mit dem .NET-Entwicklungszertifikat. Messwerte (Logins nach Ergebnis), keine personenbezogenen Daten im Log. **Tests mit einem nachgebauten Twitch** (Discovery, Token, Schlüssel), auch für manipulierte Antworten (falsches `state`, falsche Signatur, abgelaufenes Token) | ⬜ |
| 8.2 | **Creator-Anmeldung mit Twitch:** Knopf „Mit Twitch verbinden“ im Creator-Formular. Danach ist der Kanal gesetzt und gesperrt, Name und E-Mail sind vorausgefüllt (änderbar). Gespeichert werden Twitch-ID (eindeutig) und Kanalname, Migration Nr. 6. Double-Opt-in bleibt wie bisher. Im Admin-Bereich ein Kennzeichen „Twitch verifiziert“, in Liste, Detail und Export | ⬜ |
| 8.3 | **„Mein Bereich“ für Creator** (nach F-029): angemeldet mit Twitch die eigenen Daten und den Status sehen, Angaben ändern, die eigenen Daten als Datei herunterladen und alles löschen (DSGVO, Auskunft und Löschung selbst erledigen). Tests: niemand sieht fremde Daten. Datenschutzerklärung ergänzt (Platzhalter, anwaltlich prüfen lassen) | ⬜ |
| 8.4 | **Spiele von Steam laden:** Tabelle für Steam-Spiele, Import über `appdetails` (Deutsch und Englisch, Zeitlimit, schonend mit Pausen). Dazu die passende **Twitch-Kategorie** (über die Twitch-Schnittstelle, mit der Login-App, im Admin-Bereich korrigierbar), damit Spiel und Zuschauer zusammenfinden. Titelbild und bis zu 4 Screenshots landen in der Datenbank und sind damit auch im Backup. Admin-Seite „Spiele“: App-ID oder Steam-Link eingeben, laden, aktualisieren, löschen, dazu der Befehl `import-steam 730 570 …` für Testdaten. Tests mit aufgezeichneten Steam-Antworten, ohne Netz | ⬜ |
| 8.5 | **Katalog-Vorschau im Admin-Bereich** (nach F-030): Übersicht als Kacheln wie bei Steam (Bild, Name, Genres, Release, Preis oder kostenlos, Kurzbeschreibung). Detailseite mit Screenshots und Link zu Steam. Platzhalter für „Creator dabei“ und „Pot“ (I-01, I-04), deutlich als Beispiel markiert. Entwickler-Anmeldungen zeigen ihr Spiel automatisch über die Steam-App-ID, mit Knopf „von Steam laden“ | ⬜ |
| 8.6 | **Creator-Übersicht „Deine Spiele“:** In „Mein Bereich“ sehen Creator pro Spiel Stunden live, Ø- und Spitzen-Zuschauer, Zuschauerstunden und den letzten Stream, aus den Worker-Daten über ihre Twitch-ID. Bei Einnahmen steht „noch keine“. **Vorschau im Admin-Bereich:** die Übersicht für einen beliebigen Kanal aus den Worker-Daten ansehen, dazu eine Beispielrechnung (Pool 25 % für 3 Monate, Verteilung nach Zuschauerstunden, ausgedachte Verkaufszahlen, deutlich als Beispiel markiert). Damit testest du ohne eigenen Stream | ⬜ |
| 8.7 | **Abnahme:** echter Twitch-Login lokal mit deiner Login-App: anmelden, bestätigen (Mailpit), „Mein Bereich“, löschen. Der Worker läuft lokal eine Weile mit der Login-App (nicht mit der Studie), danach die Creator-Übersicht für echte Kanäle ansehen. CS2 (730), Dota 2 (570), Stardew Valley (413150), Hollow Knight (367520) und Balatro (2379780) laden und den Katalog ansehen (Screenshots). Smoke-Test ergänzt. In `IDEEN.md` notieren, was wir für I-04 gelernt haben | ⬜ |

**Was für alle Schritte gilt**
- Jeder Schritt ist ein eigener Branch und PR (F-019). Die CI ruft weder Twitch noch Steam auf. Tests laufen mit nachgebauten Antworten, damit sie ohne Netz und ohne Secrets grün sind.
- Neue Zähler starten mit 0 (E-059). Neue Texte stehen in `.resx`, Deutsch und Englisch.
- Vor 8.1 legst du die Twitch-App für den Login an (Checkliste, etwa 2 Minuten). Lokal darf auch der Worker sie benutzen, die Studie bleibt unberührt (F-001).

## Phase 9 – VPS und CD: Staging, dann Production ⏸️
**Blockiert:** wartet, bis du den Umzug in die Cloud startest (F-022 bis F-025, F-027 zurückgestellt, E-060). Spätestens Anfang Januar 2027 beginnen, damit Production vor dem Next Fest sicher läuft.

Ziel: Ein Merge auf `main` landet automatisch auf Staging (`staging.pubpot.games`). Ein Tag `v*` landet nach deiner Freigabe auf Production (`pubpot.games`). Schlägt ein Deploy fehl, rollt der Server von selbst auf den vorherigen Stand zurück. Monitoring, Alarme und Backups aus Phase 7 laufen auf dem VPS, die Backups liegen zusätzlich außerhalb des Servers.

**So sieht es auf dem Server aus (Vorschlag, wird beim Start der Phase entschieden):**
- **Ein VPS, zwei Compose-Projekte:** `pubpot-production` und `pubpot-staging`. Beide haben eine eigene Datenbank (eigenes Volume), ein eigenes Netz und eine eigene `.env`. Staging hat nur Testdaten, keinen Worker und schickt Mails nur an sein eigenes Mailpit.
- **Ein Caddy für beide Domains:** Nur ein Prozess kann die Ports 80 und 443 belegen. Der Caddy des Production-Projekts leitet `pubpot.games` an Production und `staging.pubpot.games` an Staging weiter, `www` auf die Hauptdomain. Staging bekommt `X-Robots-Tag: noindex`, damit Suchmaschinen es nicht aufnehmen.
- **Jede Umgebung ist ein Git-Checkout** (`/opt/pubpot/production`, `/opt/pubpot/staging`) auf genau dem Commit, dessen Images laufen. Konfiguration und Image passen so immer zusammen, und ein Rollback ist „zurück auf den vorherigen Commit“.
- **App-Secrets nur in der `.env` auf dem Server**, nicht in GitHub. GitHub kennt nur den SSH-Zugang zum Deploy. Der darf auf dem Server nichts außer dem Deploy-Skript ausführen.

| # | Schritt | Status |
|---|---|---|
| 9.1 | **Compose für staging und production, lokal testbar:** `compose.production.yaml` und `compose.staging.yaml` neben der gemeinsamen `compose.yaml`. Ein Caddy für beide Domains, `www` → Hauptdomain, Staging mit `noindex` und Mailpit (nur `127.0.0.1`). Der Smoke-Test startet in der CI beide Projekte nebeneinander und prüft, dass jede Domain bei ihrer eigenen App und Datenbank landet | ⬜ |
| 9.2 | **Deploy-Skript mit automatischem Rollback** (`scripts/deploy.sh <umgebung> <commit>`): Checkout des Commits, `IMAGE_TAG` setzen, Images ziehen, auf Production vorher ein Backup, Migrations-Bundle, `up -d --wait`, danach Smoke-Test gegen `/health/ready` über Caddy. Schlägt etwas fehl, zurück auf den letzten guten Stand und mit Fehler enden. Ein Verlauf zeigt, was wann deployt wurde. **Getestet in der CI:** ein guter Deploy, dann ein absichtlich kaputtes Image → Rollback, die Seite läuft weiter | ⬜ |
| 9.3 | **Eingeschränkter SSH-Zugang für GitHub Actions:** eigener Benutzer `deploy`, eigener Schlüssel nur für den Deploy. In `authorized_keys` ist ein fester Befehl hinterlegt (`command=…`): Ein Prüfskript lässt nur `staging` oder `production` und einen 40-stelligen Commit-SHA durch, sonst nichts (keine Shell, keine Weiterleitung). Der Host-Schlüssel des Servers steht fest in GitHub (`known_hosts`), damit sich niemand dazwischenschalten kann. Tests für das Prüfskript | ⬜ |
| 9.4 | **Anleitung VPS-Härtung und Einrichtung** (`docs/SERVER.md`) mit Prüfskript `scripts/server-check.sh`: Ubuntu 24.04 LTS, SSH nur mit Schlüssel, kein Root-Login, ufw nur 22, 80, 443 (TCP) und 443/UDP, zusätzlich die kostenlose Hetzner-Firewall. Wichtig: Docker umgeht ufw bei veröffentlichten Ports, deshalb binden alle internen Ports an `127.0.0.1`. Automatische Sicherheitsupdates mit Neustart-Fenster, Docker aus dem offiziellen Repo, Ordner und `.env` für beide Umgebungen. **Danach richtest du den VPS ein** (zusammen nach Anleitung), das Prüfskript muss grün sein | ⬜ |
| 9.5 | **Staging automatisch bei Merge auf `main`:** Job `deploy-staging` in der CI nach `publish`, GitHub-Environment `staging` mit den SSH-Secrets, immer nur ein Deploy gleichzeitig. Erster echter Deploy, `https://staging.pubpot.games` mit echtem Zertifikat, Anmeldung mit Double-Opt-in über das Staging-Mailpit (per SSH-Tunnel) | ⬜ |
| 9.6 | **Production über Tag `v*` mit Freigabe:** eigener Workflow. Er prüft, dass der Tag auf einem Commit von `main` liegt und die Images dazu in GHCR existieren. Dann wartet er auf deine Freigabe im Environment `production` und deployt mit demselben Skript. Erstes Release `v1.0.0` (Ablauf und Versionsschema in der README) | ⬜ |
| 9.7 | **Monitoring auf dem VPS:** Profil `monitoring` auf Production, Alarme an deine echte Adresse über den SMTP-Anbieter, Blackbox prüft beide Domains und das echte Zertifikat. Zugang zu Grafana, Prometheus, Alertmanager und Staging-Mailpit per SSH-Tunnel (F-009), ein Befehl dafür in der README. Externer Totmannschalter bei healthchecks.io (F-026, E-060) | ⬜ |
| 9.8 | **Backups außerhalb des Servers** (Ziel nach F-025): Nach jedem nächtlichen Backup verschlüsselt auf den externen Speicher, dort dieselbe Aufbewahrung (7 Tage täglich, 4 Wochen wöchentlich). Messwert und Alarm, wenn die externe Kopie älter als 26 Stunden ist. Ein Restore direkt aus der externen Kopie wird einmal geprüft | ⬜ |
| 9.9 | **README fertig** (Abnahmekriterien des Auftrags): Architektur mit Diagramm, lokale Entwicklung, alle Umgebungsvariablen, Deploy, Rollback, Backup und Restore, Zugang zum Monitoring. Checkliste mit allen GitHub-Secrets und Environments | ⬜ |
| 9.10 | **Abnahme:** Merge → Staging automatisch. Tag → Freigabe → Production. Ein absichtlich kaputter Deploy auf Staging rollt von selbst zurück. Ein Alarm kommt als echte Mail an. Restore aus der externen Kopie in eine Test-Datenbank | ⬜ |

**Was für alle Schritte gilt**
- Jeder Schritt ist ein eigener Branch und PR (F-019). 9.1 bis 9.4 lassen sich ohne VPS bauen und in der CI testen. Ab 9.5 brauchen wir den Server, die Domain und den SMTP-Anbieter (Rückfragen F-022 bis F-028).
- **Migrationen müssen abwärtskompatibel sein:** Nach einem Rollback läuft die alte App auf der neuen Datenbank. Spalten also erst hinzufügen und erst in einem späteren Release entfernen. Das steht als Regel in der README.
- Aus Phase 5: Pro Datenbank läuft nur eine Web-Instanz (E-037). Auf dem Server `BIND_ADDRESS=0.0.0.0` nur für Caddy, alle anderen Ports bleiben auf `127.0.0.1`.
- Was du selbst erledigen musst (Konten, Kosten, DNS), steht in `CHECKLISTE-SELBST.md`.
