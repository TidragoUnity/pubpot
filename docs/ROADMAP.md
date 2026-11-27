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
- Images heißen schon so wie später in der GitHub Container Registry (`ghcr.io/tidragounity/pubpot-web`, `…-worker`, `…-migrate`), mit Tag `${IMAGE_TAG:-local}`. Phase 6 baut und pusht sie, Phase 8 zieht sie auf dem VPS.
- Getrennte Konfiguration für staging und production kommt in Phase 8. `compose.yaml` ist dafür schon so gebaut, dass nur die `.env` wechselt.
- `C:\Timo\Docker	witch-worker` und seine Container bleiben unberührt (F-001), siehe F-016.

> Ergebnis der Abnahme: von leerem Volume bis „alles gesund“ in 46 s. Lighthouse über Caddy (`https://localhost/de` und `/de/signup/creator`, mobil und Desktop) **100/100/100/100**. Images entpackt: web 261 MB, migrate 295 MB, worker 146 MB, Caddy 92 MB. HSTS kommt für `localhost` absichtlich nicht (E-038), mit echter Domain prüfen das die Tests aus 5.1.

## Phase 6 – CI ⬜
Ziel: Jeder Pull Request wird automatisch gebaut, getestet und als Docker-Stack gestartet. Nach dem Merge auf `main` liegen fertige Images mit Commit-SHA in der GitHub Container Registry, bereit für den Deploy in Phase 8.

**Vorab von dir:** Dein GitHub-Token braucht zusätzlich die Rechte **Workflows** (Lesen und Schreiben, sonst lehnt GitHub den Push von `.github/workflows` ab), **Pull requests** (Lesen und Schreiben) und für 6.7 **Administration** (Lesen und Schreiben). Anleitung in der Checkliste.

| # | Schritt | Status |
|---|---|---|
| 6.1 | **CI-Workflow Build und Tests** (`.github/workflows/ci.yml`): bei jedem Pull Request und Push auf `main`. SDK 11 aus `global.json` plus .NET-10-Laufzeit, NuGet-Cache, dann `./scripts/check.sh`, also genau dieselben Prüfungen wie lokal (Build mit Warnungen als Fehler, Format, beide Testprojekte mit Testcontainers). Testergebnisse als Artefakt, wenn etwas fehlschlägt. Minimale Rechte (`contents: read`), Actions auf Commit-SHA festgenagelt, alte Läufe desselben PRs werden abgebrochen | ⬜ |
| 6.2 | **Images bauen und Compose-Smoke-Test:** die drei Images (web, migrate, worker) mit Buildx und Build-Cache bauen, in PRs ohne Push. Danach `docker compose up --wait` mit Wegwerf-Passwörtern: migrate muss mit 0 enden, `https://localhost/health/ready` und `/de` müssen 200 liefern, `migrate-twitch` läuft auch. Bei Fehlern landen die Container-Logs im Lauf | ⬜ |
| 6.3 | **Push nach GHCR** nur bei Merge auf `main` und nur, wenn 6.1 und 6.2 grün sind: Tags `<commit-sha>` (unveränderlich, für den Deploy) und `main` (zeigt immer auf den neuesten Stand). OCI-Labels (Quelle, Commit), SBOM und Herkunftsnachweis (Provenance) am Image. Schreibrecht für Packages nur in diesem Job | ⬜ |
| 6.4 | **Image-Scan mit Trivy:** sucht in den drei Images nach bekannten Sicherheitslücken. Ergebnis als Tabelle in der Zusammenfassung des Laufs, rot nur bei **kritischen** Lücken, für die es schon ein Update gibt | ⬜ |
| 6.5 | **Dependabot** (`.github/dependabot.yml`): NuGet, Dockerfiles, Compose-Dateien und GitHub Actions, wöchentlich, Updates gebündelt. .NET-, EF-Core- und Npgsql-Pakete sowie die Laufzeit-Images bleiben auf 10.x (E-007), Postgres auf 16. Dazu „Dependabot security updates“ einschalten | ⬜ |
| 6.6 | **CodeQL** (Code-Scanning für C#, für öffentliche Repos kostenlos): findet typische Sicherheitsfehler im Code, bei PRs und wöchentlich. Funde erscheinen im Reiter „Security“ | ⬜ |
| 6.7 | **Regeln für `main`** (Ruleset): Änderungen nur per Pull Request, die Prüfungen aus 6.1, 6.2 und 6.6 müssen grün sein, kein Force-Push, kein Löschen. Ab hier arbeiten wir mit Pull Requests (siehe F-019). README: CI-Badge und Abschnitt „CI“ | ⬜ |
| 6.8 | **Abnahme:** Test-PR mit absichtlichem Formatfehler → rot und Merge gesperrt. Korrigiert → grün → Merge → Images mit SHA in GHCR. Lokal die Images aus GHCR ziehen und starten (`IMAGE_TAG=<sha>`, ohne Build). Erste Dependabot-PRs laufen durch die CI | ⬜ |

**Was für alle Schritte gilt**
- **Kosten:** keine. GitHub Actions, GHCR, Dependabot, Secret Scanning und CodeQL sind für öffentliche Repos kostenlos.
- **Lokal = CI:** Die CI ruft dieselben Befehle auf wie du (`check.sh`, `docker compose`). Was lokal grün ist, ist es auch in der CI und umgekehrt.
- **Keine Secrets in der CI:** Phase 6 braucht nur das automatische `GITHUB_TOKEN`. Die Passwörter für den Smoke-Test entstehen im Lauf und verschwinden mit ihm. Echte Secrets (SSH, Server) kommen erst in Phase 8.
- **Lieferkette:** Fremde Actions sind auf einen Commit-SHA festgenagelt, nicht auf ein Tag wie `v4`, das der Autor jederzeit umbiegen kann. Dependabot hält die SHAs aktuell.
- Secret Scanning und Push Protection sind im Repo schon an (geprüft).

> **Phasen 7 und 8 sind gegenüber dem Auftrag getauscht** (E-013): Erst läuft alles lokal, dann kommt der VPS.

## Phase 7 – Monitoring, Alarme, Backups (lokal) ⬜
Aus Phase 5 mitnehmen: Auch eine unvollständige Worker-Runde schreibt den Heartbeat (E-036). Der Alarm „Worker ohne Runde“ sollte vollständige Runden zählen.
Grob: JSON-Logs ohne personenbezogene Daten · OpenTelemetry + `/metrics` · Prometheus, Grafana mit Start-Dashboard, node-exporter, cAdvisor · Uptime Kuma + Push-URL im Worker · Alarme per E-Mail, lokal an Mailpit (E-014) · nächtliche Backups mit Rotation in einen lokalen Ordner, Restore-Skript, dokumentierter Restore-Test. Alle Oberflächen nur an `127.0.0.1`.

## Phase 8 – VPS und CD: Staging, dann Production ⬜
Aus Phase 5 mitnehmen: Staging neben Production auf demselben VPS braucht ein eigenes `EDGE_SUBNET` und einen eigenen Compose-Projektnamen. Pro Datenbank läuft nur eine Web-Instanz (E-037). Auf dem Server `BIND_ADDRESS=0.0.0.0`, Firewall auch für 443/UDP.
Grob: vorher F-008 klären (VPS, Backup-Ziel, Domain, SMTP) · Anleitung VPS-Härtung · Compose-Konfiguration für staging/production · Deploy-Skript (pull, migrate, up, Smoke-Test, automatischer Rollback) · Staging bei Merge auf `main` · Production über Tag `v*` mit manueller Freigabe · Monitoring auf dem VPS, Zugang per SSH-Tunnel (F-009) · Backups auf externen Speicher · README fertig.
