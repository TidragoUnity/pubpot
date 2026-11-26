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

## Stand (2026-11-26)
- **Phasen 0–3 fertig:** alle Seiten auf DE/EN, Formulare für Entwickler und Creator, Datenbank, Double-Opt-in mit Mail-Outbox, Spam-Schutz, Aufräum-Job, Health-Checks.
- 272 Tests grün, 3 Migrationen. Lighthouse auf allen Seiten 100/100/100/100.
- **Phase 4 läuft:** 4.1–4.2 sind fertig (Hash-Befehl, Login mit Sperre, E-025/E-026). Weiter geht es mit **4.3 Datenmodell Pipeline**. **Keine offenen Rückfragen.**

## Nächste Aufgabe
### Phase 4 – Admin-Bereich: weiter mit dem nächsten ⬜-Schritt in `docs/ROADMAP.md`
- Der Plan mit allen Schritten steht in `docs/ROADMAP.md`. Die Anforderungen stehen in `docs/AUFTRAG.md` im Abschnitt „Admin-Bereich“.
- Der Admin-Bereich ist **nur auf Deutsch**: Die Texte stehen direkt in Razor, nicht in `.resx`, und es gibt keine Sprach-Route (E-024).
- Benachrichtigungen enthalten keine persönlichen Daten. Discord ist aus, solange die Umgebungsvariable fehlt (E-014).
- Aufbewahrungsfrist (4.9): Nur eine **Änderung** im Admin-Bereich zählt als Aktivität, auch „Pilot“ bekommt nach 12 Monaten die Warnmail (E-023).

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
| `src/PubPot.Web/Data` | `AppDbContext`, Entitäten (`Signup`, `SignupConfirmation`, `OutboxEmail`), Migrationen, `DataCleanup` |
| `src/PubPot.Web/Signups` | Geschäftslogik der Anmeldung (`SignupService`, Token, Einwilligung, Validierung) |
| `src/PubPot.Web/Email` | Outbox-Versand per SMTP (`OutboxProcessor`) |
| `src/PubPot.Web/Spam` | Honeypot, Mindestzeit, Rate-Limiting, Basisklasse `SignupFormPageModel` |
| `src/PubPot.Web/Pages` | Razor Pages: Start, Signup/*, Legal/*, Status/* |
| `src/PubPot.Web/Resources` | Texte DE (`.resx`) und EN (`.en.resx`) |
| `src/PubPot.Web/Admin` | Admin-Konto: `AdminOptions`, `AdminPassword` (Hash), `HashPasswordCommand`, `AdminAuth` (Cookie, Sitzungen, `UseAdminGate`), `AdminLoginThrottle` |
| `src/PubPot.Web/Pages/Admin` | Admin-Seiten (nur Deutsch, eigenes Layout, `wwwroot/css/admin.css`) |
| `src/PubPot.Web/Security` | Security-Header und CSP |
| `src/TwitchWorker` | Twitch-Worker (.NET 10) |
| `tests/PubPot.Web.Tests` | Integrationstests (`PubPotWebFactory` ohne DB, `DatabaseWebFactory` mit Testcontainers) |
| `deploy/` | `compose.yaml` für die lokalen Dienste (Postgres, Mailpit :8025, Adminer :8081) |

## Schnellbefehle
```bash
cd deploy && docker compose up -d          # lokale Dienste (Docker muss laufen)
./scripts/check.sh                         # Build + Format + Tests wie in der CI
dotnet run --project src/PubPot.Web        # http://localhost:5080
```
