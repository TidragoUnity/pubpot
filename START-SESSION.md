# Sitzungsstart PubPot

**Zu Beginn jeder Sitzung nur diese Datei lesen.** Sie enthält das Nötige, um loszulegen.
Andere Dateien nur dann öffnen, wenn die aktuelle Aufgabe sie braucht (siehe „Wo steht was“).
Am Ende jeder Sitzung die Abschnitte **Stand** und **Nächste Aufgabe** aktualisieren.

---

## Projekt in Kürze
- **PubPot** ist eine Landingpage auf Deutsch und Englisch. Spieleentwickler und Creator (Streamer) melden sich dort an, mit Double-Opt-in. Ein Admin-Bereich für die Akquise kommt noch dazu.
- Mit im Repo liegt der **Twitch-Worker**, der regelmäßig Stream-Daten von Twitch in PostgreSQL sammelt.
- **Lernprojekt:** Der Nutzer lernt damit Docker, CI/CD und Monitoring. C#/.NET und Unity kennt er schon.
- **Repo:** `C:\Timo\Docker\pubpot`, öffentlich auf GitHub als `TidragoUnity/pubpot` (Showcase).
- **Technik:** .NET 10, gebaut mit SDK 11 (`global.json`), ASP.NET Core Razor Pages, EF Core mit PostgreSQL 16, MailKit, xUnit v3 und Testcontainers. Eigenes CSS ohne externe Quellen, strikte CSP.

## Regeln (Kurzfassung, verbindlich ist `CLAUDE.md`)
- Auf **Deutsch** antworten, Code-Bezeichner auf Englisch.
- **Neue Phase:** erst fein in `docs/ROADMAP.md` planen, dem Nutzer vorlegen und auf sein **OK** warten.
- **Ein Roadmap-Schritt ergibt einen lauffähigen Commit**, Nachricht `x.y: Was`. Mit jedem Commit `ROADMAP.md` (Status), `JOURNAL.md` (neuer Eintrag oben) und diese Datei aktualisieren.
- **Vor jedem Commit** `./scripts/check.sh` ausführen (Build, Format, Tests). Befehle mit `&&` verketten und den Exit-Code prüfen. In 3.7 ging deshalb ein Commit mit Fehler durch.
- **Unklarheiten, Kosten oder Abweichungen vom Auftrag:** Frage `F-0xx` in `docs/RUECKFRAGEN.md` stellen und auf die Antwort warten.
- **Wichtige Entscheidungen** kurz erklären, weil der Nutzer lernt, und als `E-0xx` in `docs/ENTSCHEIDUNGEN.md` begründen.
- **Das Repo ist öffentlich:** keine Secrets und keine echten persönlichen Daten. `docs/AUFTRAG.md` bleibt nur lokal (gitignored).
- **`C:\Timo\Docker\twitch-worker` nicht anfassen**, dort läuft eine Studie (F-001).
- **Git:** Commits gehen über die anonyme GitHub-Adresse (`…@users.noreply.github.com`). Wegen eines Zertifikatsproblems ist `http.sslBackend schannel` gesetzt. Push mit `gh`, das Token läuft ca. am 24.02.2027 ab.
- **CSP:** kein `style="…"` und kein Inline-`<script>`. Styles kommen nach `wwwroot/css/site.css`, Skripte als Datei nach `wwwroot/js`.
- **Texte** stehen immer in `.resx`, Deutsch und Englisch (`*.en.resx`). Der Paritätstest meldet fehlende Übersetzungen.

## Stand (2026-11-27)
- **Phasen 0–7 fertig:** Website DE/EN mit Anmeldung, Double-Opt-in, Spam-Schutz, Admin-Bereich, Aufbewahrungsfrist. Docker-Stack hinter Caddy (`https://localhost`).
- **Phase 6 (CI):** Jeder PR durchläuft „Build und Tests“ (`check.sh`), „Images und Smoke-Test“ (`smoke-test.sh`, Trivy) und CodeQL. Merges auf `main` legen die Images öffentlich in GHCR ab (`ghcr.io/tidragounity/pubpot-{web,migrate,worker}:<sha>`). Dependabot läuft wöchentlich. `main` ist geschützt: nur PRs, nur Squash, nur mit grüner CI (E-040 bis E-048).
- **Phase 7 (Monitoring, lokal):**
  - JSON-Logs ohne personenbezogene Daten (E-050).
  - `/metrics` für Website und Worker auf Port 9464, nur intern (E-051, E-052).
  - Profil `monitoring`: Prometheus, Grafana mit Start-Dashboard, Alertmanager, Blackbox, node-exporter, cAdvisor (E-053 bis E-055). Elf Alarmregeln mit Unit-Tests.
  - Dienst `backup` nachts mit `scripts/restore.sh` (E-056). Restore-Test im Smoke-Test (E-057).
  - Abnahme: jeder Alarm ausgelöst, Mails in Mailpit (E-058). Die README hat den Abschnitt „Monitoring, Alarme und Backups“.
  - 7.10: Eigene Zähler starten mit 0, damit `increase()` das erste Ereignis sieht. „Anmeldungen pro Tag“ als Tagesbalken (E-059).
- 431 Tests, 15 Regel-Tests, Smoke-Test mit 25 Prüfungen, 5 Migrationen.
- **Keine offenen Rückfragen.** Ideen für nach V1 stehen in `docs/IDEEN.md` (I-01 bis I-04).

## Nächste Aufgabe
1. **Phase 8 – VPS und CD (Staging, dann Production)** ⚠️ erst beginnen, wenn der Nutzer ausdrücklich bestätigt. Vorher F-008 klären (VPS, Backup-Ziel außerhalb des Servers, Domain, SMTP), dann in `docs/ROADMAP.md` fein planen und vorlegen. Mitzunehmen aus Phase 7:
   - externer Dienst für Server-Ausfall und Worker über `Worker__PushUrl` (E-052, E-058)
   - Backups zusätzlich außerhalb des Servers (E-056)
   - Monitoring per SSH-Tunnel
   - `GRAFANA_ADMIN_PASSWORD` und `ALERT_EMAIL` auf dem Server

**Arbeitsweise seit Phase 6 (F-019, E-040):** Jeder Schritt in einem eigenen Branch (`x.y-kurzname`), PR, warten bis grün (`gh pr checks <nr> --watch`), dann `gh pr merge <nr> --squash --subject "x.y: …"`. Direkt auf `main` geht nicht mehr. Push und Merge sind in `C:\Timo\Docker\.claude\settings.local.json` freigegeben.

**Hilfreich zu wissen:** Lokal entweder `dotnet run` (dann nur `docker compose up -d db mailpit adminer`) oder der Web-Container, nicht beides gegen dieselbe Datenbank (E-037). Beim Testlauf erscheint zweimal „An error occurred while reading the key ring“. Das ist harmlos und kommt aus den Migrations-Tests aus Phase 4.

## Wo steht was
| Datei | Wann lesen |
|---|---|
| `CLAUDE.md` | Arbeitsregeln (wird automatisch geladen) |
| `docs/ROADMAP.md` | beim Planen einer Phase oder zum Aktualisieren des Status |
| `docs/JOURNAL.md` | nur den obersten Eintrag, wenn Details zur letzten Arbeit fehlen |
| `docs/RUECKFRAGEN.md` | wenn der Nutzer eine Frage beantwortet oder eine neue nötig ist |
| `docs/ENTSCHEIDUNGEN.md` | nur die E-Nummer, auf die verwiesen wird (`grep "## E-0xx"`) |
| `docs/AUFTRAG.md` | nur den passenden Abschnitt (z. B. „Admin-Bereich“, „Docker“) |
| `docs/IDEEN.md` | Ideen für die Plattform nach V1 (I-0xx), nur wenn der Nutzer eine Idee ausarbeiten will |
| `docs/CHECKLISTE-SELBST.md` | Aufgaben für den Nutzer (Konten, Geld, Recht), bei Bedarf ergänzen |
| `README.md` | lokales Starten, Befehle, Ports |

## Code-Landkarte
| Ordner | Inhalt |
|---|---|
| `src/PubPot.Web/Data` | `AppDbContext`, Entitäten (`Signup` mit Pipeline-Feldern, `SignupConfirmation`, `OutboxEmail`), Migrationen, `DataCleanup` |
| `src/PubPot.Web/Signups` | Geschäftslogik der Anmeldung (`SignupService`, Token, Einwilligung, Validierung) |
| `src/PubPot.Web/Email` | Outbox-Versand per SMTP (`OutboxProcessor`) |
| `src/PubPot.Web/Spam` | Honeypot, Mindestzeit, Rate-Limiting, Basisklasse `SignupFormPageModel` |
| `src/PubPot.Web/Pages` | Razor Pages: Start, Signup/*, Legal/*, Status/* |
| `src/PubPot.Web/Resources` | Texte DE (`.resx`) und EN (`.en.resx`) |
| `src/PubPot.Web/Admin` | Admin-Konto: `AdminOptions`, `AdminPassword` (Hash), `HashPasswordCommand`, `AdminAuth` (Cookie, Sitzungen, `UseAdminGate`), `AdminLoginThrottle` |
| `src/PubPot.Web/Pages/Admin` | Admin-Seiten (nur Deutsch, eigenes Layout, `wwwroot/css/admin.css`) |
| `src/PubPot.Web/Notifications` | Hinweise an das Team: `SignupNotifier` (Mail über die Outbox), `DiscordWebhook` (Warteschlange + Hintergrunddienst) |
| `src/PubPot.Web/Security` | Security-Header und CSP |
| `src/PubPot.Web/Monitoring` | Metriken: `PubPotMetrics` (eigene Zähler), `MetricsEndpoint` (`/metrics` nur auf Port 9464) |
| `src/TwitchWorker` | Twitch-Worker (.NET 10), `WorkerMetrics` (/metrics per HttpListener), `PushNotifier` |
| `tests/PubPot.Web.Tests` | Integrationstests (`PubPotWebFactory` ohne DB, `DatabaseWebFactory` mit Testcontainers) |
| `tests/TwitchWorker.Tests` | Worker: Healthcheck, Schema-Runner `deploy/db/twitch/migrate.sh` im echten Postgres |
| `deploy/` | `compose.yaml` (db, migrate, web, backup (Skript in `backup/`), Profil `worker`: migrate-twitch, worker, Profil `monitoring`: prometheus, alertmanager, grafana, blackbox, node-exporter, cadvisor; Konfiguration in `monitoring/`), `compose.override.yaml` (Build, Mailpit :8025, Adminer :8081), `caddy/Caddyfile` (https://localhost), `db/twitch` (Worker-Schema) |

## Schnellbefehle
```bash
cd deploy && docker compose up -d db mailpit adminer   # Hilfsdienste für dotnet run
cd deploy && docker compose up -d --build              # alles in Docker (nicht zusammen mit dotnet run)
./scripts/check.sh                         # Build + Format + Tests wie in der CI
dotnet run --project src/PubPot.Web        # http://localhost:5080
```
