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
- **Phasen 0–4 fertig:** alle Seiten auf DE/EN, Formulare, Datenbank, Double-Opt-in mit Mail-Outbox, Spam-Schutz, Aufräum-Job, Health-Checks. Dazu der **Admin-Bereich** (`/admin`, nur Deutsch): Login mit Sperre, Liste mit Filter und Suche, Detailseite mit Pipeline, CSV-Export, Löschen (DSGVO), Hinweis-Mail bei neuer Anmeldung (Discord optional) und die **Aufbewahrungsfrist** mit Warnmail und Link „Ich möchte dabei bleiben“.
- 401 Tests grün, 5 Migrationen. Lighthouse auf allen Seiten 100 (Performance, Barrierefreiheit, Best Practices).
- **Phase 5 in Arbeit** (Schritte 5.1–5.8 in der Roadmap, F-016 und F-017 beantwortet, E-032). Fertig: 5.1 Forwarded Headers (E-033), 5.2 Dockerfile Web (E-034), 5.3 Migrations-Bundle (E-035), 5.4 Worker (E-036), 5.5 Compose (E-037). **Keine offenen Rückfragen.**

## Nächste Aufgabe
### Phase 5 – Docker und Compose lokal (freigegeben, Schritt für Schritt umsetzen)
Nächster offener Schritt laut `docs/ROADMAP.md`. Jeder Schritt ist ein eigener Commit.
- **Worker nur mit Profil `worker`** (F-016). Bis zum Ende der Studie (ca. 03.12.) keine echte Runde gegen Twitch.
- **Lokal `https://localhost`** mit Caddys eigenem Zertifikat (F-017).
- Aus Phase 4: In die `.env.example` kommen `Admin__PasswordHash`, `Notifications__Email` und `Notifications__DiscordWebhookUrl`. `hash-password` muss im Container laufen (`docker compose run --rm --no-deps web hash-password`). `Site__BaseUrl` ist Pflicht, denn der Aufbewahrungs-Job baut Links ohne Anfrage (E-031).

**Hilfreich zu wissen:** Die Data-Protection-Schlüssel liegen schon in der Datenbank, Admin-Cookies überleben also einen Neustart. Mails sind nur Text und laufen über die Outbox, Links kommen aus `Site:BaseUrl` (E-021). Das Rate-Limiting gibt es schon in `Spam/RateLimits.cs`.

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
| `deploy/` | `compose.yaml` (db, migrate, web, Profil `worker`: migrate-twitch, worker), `compose.override.yaml` (Build, Mailpit :8025, Adminer :8081, Web :8090), `db/twitch` (Worker-Schema) |

## Schnellbefehle
```bash
cd deploy && docker compose up -d db mailpit adminer   # Hilfsdienste für dotnet run
cd deploy && docker compose up -d --build              # alles in Docker (nicht zusammen mit dotnet run)
./scripts/check.sh                         # Build + Format + Tests wie in der CI
dotnet run --project src/PubPot.Web        # http://localhost:5080
```
