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
- **Phasen 0–6 fertig:** Website DE/EN mit Anmeldung, Double-Opt-in, Spam-Schutz, Admin-Bereich, Aufbewahrungsfrist. Docker-Stack hinter Caddy (`https://localhost`).
- **Phase 6 (CI):** Jeder PR durchläuft „Build und Tests“ (`check.sh`), „Images und Smoke-Test“ (`smoke-test.sh`, Trivy) und CodeQL. Merges auf `main` legen die Images öffentlich in GHCR ab (`ghcr.io/tidragounity/pubpot-{web,migrate,worker}:<sha>`). Dependabot läuft wöchentlich. `main` ist geschützt: nur PRs, nur Squash, nur mit grüner CI (E-040 bis E-048).
- 404 Tests grün, 5 Migrationen. MailKit 5.0 mit SMTP-Test gegen Mailpit (ersetzt Dependabot-PR #7).
- **Keine offenen Rückfragen.** Ideen für nach V1 stehen in `docs/IDEEN.md` (I-01 bis I-04).

## Nächste Aufgabe
1. **Phase 7 – Monitoring, Alarme, Backups (lokal)** ⚠️ erst beginnen, wenn der Nutzer ausdrücklich bestätigt. Vorher in `docs/ROADMAP.md` fein planen und vorlegen. Mitzunehmen ist der Punkt aus Phase 5: Auch unvollständige Worker-Runden schreiben den Heartbeat (E-036).

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
| `src/TwitchWorker` | Twitch-Worker (.NET 10) |
| `tests/PubPot.Web.Tests` | Integrationstests (`PubPotWebFactory` ohne DB, `DatabaseWebFactory` mit Testcontainers) |
| `tests/TwitchWorker.Tests` | Worker: Healthcheck, Schema-Runner `deploy/db/twitch/migrate.sh` im echten Postgres |
| `deploy/` | `compose.yaml` (db, migrate, web, Profil `worker`: migrate-twitch, worker), `compose.override.yaml` (Build, Mailpit :8025, Adminer :8081), `caddy/Caddyfile` (https://localhost), `db/twitch` (Worker-Schema) |

## Schnellbefehle
```bash
cd deploy && docker compose up -d db mailpit adminer   # Hilfsdienste für dotnet run
cd deploy && docker compose up -d --build              # alles in Docker (nicht zusammen mit dotnet run)
./scripts/check.sh                         # Build + Format + Tests wie in der CI
dotnet run --project src/PubPot.Web        # http://localhost:5080
```
