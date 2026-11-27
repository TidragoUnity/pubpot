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
- **Phasen 0–5 fertig:** alle Seiten auf DE/EN, Formulare, Datenbank, Double-Opt-in mit Mail-Outbox, Spam-Schutz, Aufräum-Job, Health-Checks, Admin-Bereich (`/admin`) mit Pipeline, Export, Löschen, Hinweis-Mails und Aufbewahrungsfrist.
- **Phase 5 (Docker):** `cd deploy && docker compose up -d --build` startet alles hinter Caddy unter `https://localhost` (Zertifikatswarnung wegklicken, F-017). Images „chiseled“ ohne Shell, Nicht-Root, eingebaute Healthchecks. Migrationen als eigener Container (EF-Bundle bzw. SQL-Skripte für den Worker). Worker nur mit `--profile worker` (F-016). Forwarded Headers nur vom Netz „edge“ (E-033). Entscheidungen E-032 bis E-039.
- 402 Tests grün (`PubPot.Web.Tests`, `TwitchWorker.Tests`), 5 Migrationen. Lighthouse überall 100, auch über Caddy.
- **Phase 6 in Arbeit** (6.1–6.8, F-018 und F-019 beantwortet, E-040). Fertig: 6.1 CI-Workflow (E-041), 6.2 Images und Smoke-Test (E-042), 6.3 Push nach GHCR (E-043). **Keine offenen Rückfragen.**

## Nächste Aufgabe
### Phase 6 – CI (geplant und freigegeben, Schritt für Schritt umsetzen)
Nächster offener Schritt laut `docs/ROADMAP.md`. **Ab 6.1 jeder Schritt in einem eigenen Branch** (`x.y-kurzname`) mit Pull Request. Ist die CI grün, mergt Claude per „Squash and merge“ mit dem Titel `x.y: …` (F-019, E-040).
- Images in GHCR werden öffentlich (F-018). Nach dem ersten Push stellt der Nutzer jedes Image einmal in der Weboberfläche um.
- Das Token braucht die Rechte Workflows, Pull requests und Administration (Checkliste). Lehnt GitHub den Push von `.github/workflows` ab, fehlt das Recht „Workflows“.

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
