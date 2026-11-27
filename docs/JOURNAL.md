# Journal

Chronologisches Protokoll: was gemacht wurde, was als Nächstes kommt. Neueste Einträge oben.
Damit kann jede neue Arbeitssitzung nahtlos weitermachen.

---

## 2026-11-27 · Schritt 7.2 – Metriken der Website
- **OpenTelemetry mit Prometheus-Exporter.** Erfasst werden:
  - die eingebauten Messwerte von .NET: Anfragen, Antwortzeiten, Rate-Limiting, Ausnahmen, Speicher, GC, Threads und ausgehende HTTP-Aufrufe
  - eigene Zähler: Anmeldungen und Bestätigungen nach Art, Spam nach Grund, verschickte und fehlgeschlagene Mails, gelöschte Einträge nach Typ (E-051)
- **`/metrics` auf Port 9464,** geprüft am echten Port der Verbindung, nicht am Host-Header. Lokal mit `dotnet run`: http://localhost:9464/metrics.
- **Tests:**
  - Port-Trennung, gefälschter Host-Header, Format.
  - Zähler über `MetricCollector`, ohne personenbezogene Daten.
  - Smoke-Test: `/metrics` von außen 404, auch mit `Host: localhost:9464`, im Docker-Netz erreichbar.
- **Gefunden:** Mehrere Test-Apps im selben Prozess stören sich beim OpenTelemetry-Export (gleichnamige Zähler werden zusammengefasst). Deshalb messen die Tests die Zähler direkt.
- Stand: 416 Tests grün.
- **Als Nächstes:** 7.3 Metriken des Workers und Push-URL.

## 2026-11-27 · Schritt 7.1 – JSON-Logs ohne personenbezogene Daten
- **Format:** Web und Worker loggen im Container als JSON, eine Zeile pro Eintrag, Zeit in UTC, ohne Scopes. Bei `dotnet run` bleibt die lesbare Ausgabe, jetzt mit Uhrzeit (E-050).
- **Log-Rotation** für alle Dienste in `compose.yaml`: höchstens 5 × 10 MB pro Container.
- **Tests:**
  - `LogFormatTests` schreibt einen Eintrag mit den echten appsettings von Web und Worker und prüft das JSON, auch mit mehrzeiligem Stacktrace.
  - `LogPrivacyTests` spielt den ganzen Ablauf durch und sucht im Log nach E-Mail, Name, Kanal, IP, Token, Passwort, Cookie und Notiz. Nichts gefunden. Gegenprobe mit absichtlichem Leck: Der Test schlägt an.
  - Der Smoke-Test prüft, dass das Web-Log im Container nur aus JSON-Zeilen besteht.
- Beim Durchsehen: Der bestehende Code hielt sich schon an die Regel. Der Mail-Ausgang loggt nur die Mail-ID, Discord nur den Statuscode, und der HttpClient für den Webhook loggt die URL nicht.
- Stand: 409 Tests grün.
- **Als Nächstes:** 7.2 Metriken der Website.

## 2026-11-27 · Phase 7 geplant
- Phase 7 in 9 Schritte aufgeteilt (7.1 JSON-Logs bis 7.9 Abnahme).
- F-020: Prometheus-Endpunkt mit dem Vorschau-Paket von OpenTelemetry. F-021: Blackbox-Exporter statt Uptime Kuma (E-049).
- Go vom Nutzer am 2026-11-27, Plan per PR auf `main`.

## 2026-11-27 · Ideensammlung für die Zeit nach V1
- Neue Datei `docs/IDEEN.md`, verlinkt aus ROADMAP und START-SESSION. Darin stehen die Ideen des Nutzers, mit ersten Gedanken und offenen Punkten, damit sie später als Entwurf ausgearbeitet werden können:
  - I-01: Creator sehen, wer ein Spiel noch bewirbt.
  - I-02: Entwickler bündeln ihre Pools.
  - I-03: Steam-Keys direkt an Creator.
  - I-04: Spielekatalog mit Kreisdiagramm und Pot.
- Keine der Ideen ist bisher geplant, weil V1 nur die Landingpage umfasst. Deshalb ist auch I-04 aufgenommen.

## 2026-11-27 · MailKit 5.0 (ersetzt Dependabot-PR #7)
- MailKit 4.18.2 → 5.0.0. MailKit braucht jetzt kein BouncyCastle mehr, sondern nur noch `MimeKit.Core`. Damit sind weniger Pakete im Image und weniger Angriffsfläche.
- Anpassung: In MailKit 5 sind `Send`/`SendAsync(message)` Erweiterungsmethoden. Es fehlte nur `using MailKit;` in `Email/IEmailTransport.cs`.
- **Neuer Test `SmtpTransportTests`:** schickt per Testcontainers echt über SMTP an Mailpit und prüft über dessen API Betreff mit Umlauten, Absender, Empfänger und Text. Dazu der Fall „Server nicht erreichbar“, der eine Ausnahme werfen muss, damit der Mail-Ausgang es erneut versucht. Bisher ersetzten alle Tests den Versand durch eine Attrappe, also blieb genau der Teil ungeprüft, der von MailKit abhängt. Der Test lief vorher auch gegen 4.18.2 grün, prüft also dasselbe Verhalten vor und nach dem Update.
- **Gefunden beim lokalen Prüfen:** Nach mehrfachem Wechsel der Paketversion lieferte der Razor-Generator 736 Fehler in unveränderten `.cshtml`-Dateien. Ursache war ein hängender Build-Server bzw. ein veralteter `obj`-Stand, nicht der Code. Abhilfe: `dotnet build-server shutdown` und `bin`/`obj` löschen. In der CI kann das nicht passieren, weil jeder Lauf frisch startet.
- Stand: 404 Tests grün.

## 2026-11-27 · Schritt 6.8 – Abnahme, Phase 6 abgeschlossen
- **Direkter Push auf `main`** mit einem Test-Commit: abgelehnt durch die Regeln.
- **Test-PR #9 mit absichtlichem Formatfehler** (falsch eingerückte Zeile): „Build und Tests“ rot, Merge-Status `BLOCKED`, `gh pr merge` abgewiesen. Danach korrigiert (dieser Commit), grün, gemergt.
- **GHCR:** Seit 6.3 legt jeder Merge die Images mit SHA- und `main`-Tag ab, öffentlich, mit SBOM und Provenance. **Geprüft nach dem Merge:** `IMAGE_TAG=edefb70… ./scripts/smoke-test.sh` hat die Images dieses Commits aus GHCR gezogen (ohne lokalen Build) und alle 5 Prüfungen bestanden.
- **Erster Dependabot-PR #7 (MailKit 5.0.0):** Die CI ist rot, weil MailKit 5 die Send-API geändert hat. Die CI hat ihren Zweck also gleich beim ersten Update erfüllt. Behoben wird es in einem eigenen PR, mit einem neuen Test für den echten SMTP-Versand (E-048).
- Stand: 402 Tests, 5 Migrationen, 9 PRs in Phase 6.
- **Als Nächstes:** PR für MailKit 5. Danach Phase 7 (Monitoring, Alarme, Backups lokal), vorher planen und deine Bestätigung abwarten.

## 2026-11-27 · Schritt 6.7 – Regeln für `main`
- Ruleset „main schützen“ per API angelegt, Quelle `.github/rulesets/main.json`: nur PR, nur Squash, lineare Historie, kein Force-Push und kein Löschen, 5 Pflicht-Prüfungen (nur von GitHub Actions), Code Scanning als Sperre, keine Ausnahmen (E-047).
- Repo-Einstellungen: nur Squash-Merge mit PR-Titel als Commit-Titel, Branches werden nach dem Merge gelöscht.
- README: Badges für CI und CodeQL, neuer Abschnitt „CI und Arbeitsablauf“ mit Workflows und Ablauf für eine Änderung. Die Ordnertabelle ist ergänzt.
- Dieser PR ist der erste, der selbst durch die Regeln muss.
- **Als Nächstes:** 6.8 Abnahme.

## 2026-11-27 · Schritt 6.6 – CodeQL
- `.github/workflows/codeql.yml`: CodeQL für C# (mit Build), GitHub Actions und JavaScript, Regelsatz `security-extended`, bei PRs, auf `main` und wöchentlich (E-046).
- Vorher geprüft: GitHubs „Default setup“ ist aus, es gibt also keinen Konflikt mit dem eigenen Workflow.
- **Als Nächstes:** 6.7 Regeln für `main`.

## 2026-11-27 · Schritt 6.5 – Dependabot
- `.github/dependabot.yml` für NuGet, `global.json`, die Dockerfiles, die Compose-Dateien und GitHub Actions: wöchentlich, gebündelt, keine Hauptversionen für .NET, EF Core, Npgsql, das SDK, die .NET-Images und Postgres (E-045).
- Per API eingeschaltet: Dependabot-Warnungen (`vulnerability-alerts`) und automatische Sicherheitsupdates (`automated-security-fixes`).
- Die ersten Dependabot-PRs kommen nach dem Merge. Sie werden in der Abnahme (6.8) geprüft.
- **Als Nächstes:** 6.6 CodeQL.

## 2026-11-27 · Schritt 6.4 – Image-Scan mit Trivy
- Trivy prüft die drei Images im Job „Images und Smoke-Test“. Bericht mit hohen und kritischen Lücken in der Zusammenfassung, rot nur bei kritischen Lücken mit Update (E-044).
- Trivy wird über `setup-trivy` v0.3.1 in Version v0.76.2 installiert, beides unveränderliche Releases, festgenagelt. Grund ist die Kompromittierung der `trivy-action` im März 2026.
- Neu: wöchentlicher CI-Lauf montags und Start von Hand (`workflow_dispatch`).
- Vorab lokal gegen die Images in GHCR gescannt: alle drei 0 Funde, Ubuntu und .NET-Pakete erkannt.
- **Nachtrag zu 6.3:** Die ersten Images (Commit `2d7a814`) liegen in GHCR mit Tags SHA und `main`, mit SBOM und Provenance. Sie waren **sofort öffentlich** (anonym geprüft), weil die CI des öffentlichen Repos sie angelegt hat. Der Checklisten-Punkt dazu entfällt.
- **Als Nächstes:** 6.5 Dependabot.

## 2026-11-27 · Schritt 6.3 – Push nach GHCR
- Neuer Job `publish` (Matrix für web, migrate, worker): nur auf `main`, nur nach grünem Build, Tests und Smoke-Test. Tags `<commit-sha>` und `main`, mit SBOM, Provenance und OCI-Labels, Anmeldung über `GITHUB_TOKEN` (E-043).
- Im PR wird der Job übersprungen. Der erste echte Push passiert beim Merge dieses PRs, danach wird er geprüft.
- **Als Nächstes:** 6.4 Image-Scan mit Trivy.

## 2026-11-27 · Schritt 6.2 – Images bauen und Compose-Smoke-Test
- Neues Skript `scripts/smoke-test.sh`: Stack aus `deploy/compose.yaml` mit fertigen Images in einem eigenen Projekt starten, 5 Prüfungen, Logs bei Fehlern, immer aufräumen (E-042).
- CI-Job „Images und Smoke-Test“: Buildx baut web, migrate und worker mit dem Commit-SHA als Tag, mit Build-Cache. Danach läuft der Smoke-Test, die Image-Größen stehen in der Zusammenfassung.
- Lokal getestet: bestanden. Mit einem nicht vorhandenen Image-Tag endet er mit Exit 1 und lässt keine Container und Volumes zurück.
- **Als Nächstes:** 6.3 Push nach GHCR.

## 2026-11-27 · Schritt 6.1 – CI-Workflow Build und Tests
- `.github/workflows/ci.yml`: Job „Build und Tests“ bei jedem Pull Request und Push auf `main`. SDK 11 und .NET 10, NuGet-Cache, dann `./scripts/check.sh` (E-041).
- `scripts/check.sh` gliedert seine Ausgabe in GitHub Actions in einklappbare Gruppen, lokal bleibt sie wie bisher.
- Actions auf Commit-SHA festgenagelt (checkout v7.0.1, setup-dotnet v6.1.0, cache v6.1.0), nur Leserechte, alte PR-Läufe werden abgebrochen.
- Geprüft mit `actionlint` (im Docker-Container), lokal `check.sh` grün.
- **Erster Schritt im neuen Ablauf (F-019):** Branch `6.1-ci`, Pull Request, Merge bei grüner CI.
- **Abweichung:** keine Testergebnisse als Artefakt. Dafür bräuchte es ein Zusatzpaket, das Log reicht (E-041).
- **Als Nächstes:** 6.2 Images bauen und Compose-Smoke-Test.

## 2026-11-27 · Phase 6 geplant
- Phase 6 in 8 Schritte aufgeteilt (6.1 CI-Workflow bis 6.8 Abnahme), mit Trivy und CodeQL als kostenlose Ergänzungen.
- F-018: Die Images in GHCR werden öffentlich. F-019: Jeder Schritt bekommt einen eigenen Branch und Pull Request, Merge bei grüner CI (E-040).
- Vorab von dir: Token-Rechte für Workflows, Pull Requests und Administration (Checkliste).
- **Als Nächstes:** 6.1 CI-Workflow.

## 2026-11-27 · Schritt 5.8 – Abnahme, Phase 5 abgeschlossen
- **Start von null** in einem eigenen Compose-Projekt (`-p pubpot-abnahme`, frische Zufallspasswörter nur im Scratchpad). Dein Stack war dafür gestoppt, die Volumes blieben erhalten. Das Init-Skript legte beide Datenbanken an, `migrate` spielte 5 Migrationen ein (Exit 0). Bis alles `healthy` war: **46 s**.
- **Geprüft:** web und migrate laufen als UID 1654, die Datenbank als 999, Caddy als root ohne Capabilities. Alle eigenen Container sind schreibgeschützt mit `no-new-privileges`. Caddy kann `db` nicht einmal auflösen (getrennte Netze). Das Worker-Schema lief in die frische `twitch`-Datenbank.
- **Im Browser über `https://localhost`** (Zertifikatsfehler ignoriert, F-017): Weiterleitung `/` → `/de`, Creator-Anmeldung, Bestätigungslink in Mailpit mit `https://localhost`, Bestätigung, Hinweis-Mail mit HTTPS-Link zur Detailseite. Der Link führt ohne Login zum Login und danach zurück zur Detailseite. Admin-Cookie `Secure`, `HttpOnly`, `SameSite=Strict`, Sitzungscookie. Logout. Keine Konsolenfehler. Datum und Uhrzeit in deutscher Zeit (ICU und Zeitzonen im Image funktionieren).
- **Lighthouse über Caddy:** `/de` und `/de/signup/creator`, mobil und Desktop, jeweils 100/100/100/100.
- **Gefunden und behoben:** (1) Bei POSTs ohne Token wurde aus dem 429 der Statusseite ein leeres 400, weil die Statusseite selbst ein Token verlangte. Jetzt ignorieren die Statusseiten das Token (E-039, mit Test). Danach über Caddy: 5 × 400 mit Seite, dann 429, obwohl jeder POST ein anderes gefälschtes `X-Forwarded-For` hatte. Damit ist der Schutz aus 5.1 auch im echten Aufbau bestätigt. (2) Das feste Subnetz von „edge“ hätte ein zweites Projekt auf demselben Rechner verhindert. Jetzt einstellbar über `EDGE_SUBNET` (E-039).
- Danach: Abnahme-Projekt samt Volumes entfernt, dein Stack wieder wie vorher (db, Mailpit, Adminer), 3 Anmeldungen und 5 Migrationen unverändert.
- Stand: 402 Tests grün (zwei Testprojekte), 5 Migrationen.
- **Als Nächstes:** Phase 6 (CI). Vorher fein planen und deine Bestätigung abwarten.

## 2026-11-27 · Schritt 5.7 – `.env.example` und Doku
- `deploy/.env.example` vollständig: DB-Passwörter, `SITE_DOMAIN`, `BIND_ADDRESS`, `Site__BaseUrl`, SMTP, Admin (mit Befehl `docker compose run --rm --no-deps web hash-password`), Hinweise, Worker mit `COMPOSE_PROFILES`, `IMAGE_TAG`. Jeder Abschnitt sagt, was lokal optional und was auf dem Server Pflicht ist. Dazu der Hinweis auf `$` und `;` in Passwörtern.
- README: neuer Abschnitt „Alles in Docker starten“ (Ablauf, Dienste, Admin-Hash im Container, Befehle), „Caddy-Zertifikat vertrauen (optional)“ mit `certutil -user` (ohne Admin-Rechte, wieder entfernbar) und eine Tabelle aller Variablen in `deploy/.env`.
- Checkliste: Server-`.env` und Firewall für Phase 8, optional das Zertifikat vertrauen.
- **Als Nächstes:** 5.8 Abnahme von Phase 5.

## 2026-11-27 · Schritt 5.6 – Caddy
- `deploy/caddy/Caddyfile` und Dienst `caddy` (2.11.9) in `compose.yaml`: Domain aus `SITE_DOMAIN` (Standard `localhost`), Ports über `BIND_ADDRESS` (Standard `127.0.0.1`), Volumes für Zertifikate, Healthcheck über einen internen Port (E-038).
- Netz `edge` mit festem Subnetz für Caddy und web. Die App vertraut nur diesem Netz (`Proxy__KnownNetworks`). `Site__BaseUrl` ist jetzt standardmäßig `https://${SITE_DOMAIN}`. Der direkte Port 8090 ist wieder entfernt.
- **Geprüft:** HTTP → 308 → HTTPS. Die CSP enthält `upgrade-insecure-requests`, die App erkennt also HTTPS hinter Caddy. Das Antiforgery-Cookie kommt mit `secure`. Kein `Server`- und kein `Via`-Header, `/health/ready` über Caddy 200. Caddy läuft ohne Linux-Capabilities.
- **Gefunden:** (1) Caddy hängt `Via: 1.1 Caddy` an. Weg ist er erst mit `header { -Via; defer }`, denn ohne `defer` wird der Header entfernt, bevor `reverse_proxy` ihn setzt. (2) Caddy warnt bei unformatiertem Caddyfile. Formatiert mit `caddy fmt`.
- Der Headless-Browser zeigt erwartungsgemäß `ERR_CERT_AUTHORITY_INVALID`. Für die Abnahme in 5.8 startet er mit „Zertifikatsfehler ignorieren“.
- **Als Nächstes:** 5.7 `.env.example` und Doku.

## 2026-11-27 · Schritt 5.5 – Compose für die Anwendung
- `deploy/compose.yaml` hat jetzt die Dienste `migrate`, `web`, `migrate-twitch` und `worker` (die beiden letzten nur mit Profil `worker`). Die Startreihenfolge läuft über `depends_on` mit `service_healthy` bzw. `service_completed_successfully` (E-037).
- `compose.override.yaml`: Build aus dem Repo, lokale Werte für Mailpit, Web vorübergehend unter `127.0.0.1:8090` (bis 5.6 Caddy davorsteht), Ordner `sql/` und `export/` für die Auswertung der Studie im DB-Container.
- Härtung geprüft: `ReadonlyRootfs=true`, `CapDrop=[ALL]`, `no-new-privileges`, UID 1654, keine Fehler im Log.
- **Abnahme im Kleinen:** `docker compose up -d --build` → migrate „already up to date“ (Exit 0) → web `healthy`. Anmeldung im Browser über den Container, Bestätigungsmail und Hinweis-Mail in Mailpit, ohne Konsolenfehler. Test-Anmeldung danach gelöscht.
- **Worker-Profil:** `migrate-twitch` hat das Schema in die lokale `twitch`-Datenbank eingespielt. Ohne Twitch-Zugangsdaten bricht der Worker mit klarer Meldung ab, danach wieder entfernt. Es ging keine Anfrage an Twitch.
- `docker compose run --rm --no-deps web hash-password` funktioniert im Container (mit Pipe getestet).
- `deploy/.env.example` hat alle neuen Variablen, `src/TwitchWorker/.env.example` ist entfernt. Die Worker-README beschreibt den Start über `deploy/`. Wichtig: `docker compose down -v` löscht jetzt auch die PubPot-Daten. Die README nennt deshalb `TRUNCATE` für die Studiendaten.
- **Erkannt:** Der Mail-Ausgang sperrt keine Zeilen. Deshalb gilt: eine Web-Instanz pro Datenbank, lokal nicht `dotnet run` und Container gleichzeitig (README, E-037).
- **Als Nächstes:** 5.6 Caddy.

## 2026-11-27 · Schritt 5.4 – Worker: Schema-Skript und Dockerfile
- Schema als `deploy/db/twitch/001_schema.sql` mit Runner `migrate.sh` (läuft im Postgres-Image, merkt sich eingespielte Skripte in `schema_migrations`, eine Transaktion pro Skript). `CREATE TABLE` ist aus dem Worker-Code entfernt. Der Worker prüft beim Start nur noch, ob die Tabellen da sind (E-036).
- Dockerfile auf `runtime:10.0-noble-chiseled` umgestellt, Healthcheck als Befehl `healthcheck` (Heartbeat höchstens 15 Minuten alt). Die alte `src/TwitchWorker/docker-compose.yml` ist entfernt. Der Worker kommt in 5.5 in `deploy/compose.yaml`.
- **Probelauf** mit erfundenen Twitch-Zugangsdaten und einer Twitch-Adresse ins Leere, damit nichts die Studie berührt. Ohne Schema wartet der Worker mit klarer Meldung. Nach dem Skript speichert er eine leere, unvollständige Runde und ist `healthy`. Läuft als UID 1654 mit schreibgeschütztem Dateisystem (nur `/tmp` beschreibbar). Image 146 MB.
- Neues Testprojekt `tests/TwitchWorker.Tests` mit 10 Tests: Healthcheck (frisch, alt, fehlt) und Runner im echten Postgres (erster Lauf, zweiter Lauf ohne Änderung und ohne NOTICE, alte Tabellen bleiben, kaputtes Skript wird zurückgerollt).
- **Gelernt:** In Git Bash schreibt MSYS Pfade wie `/tmp` in Windows-Pfade um. Für `docker run --tmpfs /tmp` braucht es `MSYS_NO_PATHCONV=1`.
- **Als Nächstes:** 5.5 Compose für die Anwendung.

## 2026-11-27 · Schritt 5.3 – Migrations-Bundle
- Neue Stufe `migrate` im Web-Dockerfile. Sie erzeugt mit `dotnet ef migrations bundle` eine einzelne Datei `efbundle`. Image `ghcr.io/tidragounity/pubpot-migrate:local`, entpackt 295 MB (teilt sich die Basis-Schichten mit dem Web-Image).
- Neu `AppDbContextFactory`: Bundle und `dotnet ef` brauchen nur noch den Connection-String und starten nicht mehr die ganze App (E-035). `AppDbContext.Configure` bündelt die Einstellungen für App und Werkzeuge.
- **Probleme beim Bauen:** (1) `dotnet ef` braucht die .NET-10-Laufzeit, im SDK-11-Image fehlt sie. Sie wird jetzt aus dem Laufzeit-Image kopiert. (2) Zum Erzeugen braucht EF einen Connection-String. Für diesen einen Befehl gibt es einen Platzhalter, der nicht ins Image kommt.
- **Probeläufe:** leere Datenbank → 5 Migrationen, Exit 0. Zweiter Lauf → „already up to date“. Falsches Passwort → Exit 1. Entwicklungsdatenbank → nichts zu tun. Alles mit `--read-only`.
- Die Zeile „Failed executing DbCommand … __EFMigrationsHistory“ beim allerersten Lauf ist normal: Die Tabelle gibt es da noch nicht.
- 5 neue Tests (`AppDbContextFactoryTests`).
- **Als Nächstes:** 5.4 Worker (Schema-Skript, Dockerfile).

## 2026-11-27 · Schritt 5.2 – Dockerfile für die Web-App
- `src/PubPot.Web/Dockerfile`: Build mit SDK 11, Laufzeit `aspnet:10.0-noble-chiseled-extra` (ohne Shell, ohne Paketmanager, Nicht-Root). Healthcheck mit dem neuen Befehl `healthcheck` (E-034).
- Probelauf gegen die Entwicklungsdatenbank: `/health`, `/health/ready` und `/de` liefern 200, die deutsche Seite rendert (ICU funktioniert). Der Container läuft als UID 1654 und ist nach dem ersten Healthcheck `healthy`. `docker exec … sh` schlägt fehl, eine Shell gibt es wirklich nicht. Image entpackt 261 MB.
- **Gefunden:** Npgsql suchte `libgssapi_krb5.so.2`. Behoben wie beim Worker mit `GssEncryptionMode=Disable`.
- 9 neue Tests (`HealthCheckCommandTests`): Port aus `ASPNETCORE_HTTP_PORTS`, gesund → 0, Fehlerstatus oder nicht erreichbar → 1.
- **Als Nächstes:** 5.3 Migrations-Bundle.

## 2026-11-27 · Schritt 5.1 – Forwarded Headers
- Neue Einstellung `Proxy__KnownNetworks` (z. B. `172.30.0.0/24`). Nur Anfragen aus diesen Netzen dürfen der App die Client-IP (`X-Forwarded-For`) und HTTPS (`X-Forwarded-Proto`) mitteilen (E-033). Ohne Eintrag ist die Middleware aus, sonst würde ASP.NET jedem Absender glauben.
- Die Middleware läuft als Erstes, vor Security-Headern, HSTS, Cookies und Rate-Limiting.
- 12 neue Tests (`ForwardedHeadersTests`): HSTS und Secure-Cookie über den vertrauten Proxy, auch mit IPv4-in-IPv6-Adresse (`::ffff:…`). Fremde Absender werden ignoriert, ebenso alle, wenn kein Netz eingetragen ist. Hinter dem Proxy hat jeder Besucher sein eigenes Rate-Limit, ein gefälschtes `X-Forwarded-For` holt kein neues. Ungültige Netze brechen den Start ab.
- **Gegenprobe:** Mit abgeschalteter Middleware schlagen genau die 3 Tests für den vertrauten Proxy fehl.
- Da TestServer keine Absender-IP kennt, setzt ein kleiner Test-Filter (`IStartupFilter`) sie aus einem Test-Header.
- **Als Nächstes:** 5.2 Dockerfile für die Web-App.

## 2026-11-27 · Phase 5 geplant
- Phase 5 in 8 Schritte aufgeteilt (5.1 Forwarded Headers bis 5.8 Abnahme) und von dir freigegeben.
- F-016: Der Worker startet nur mit dem Compose-Profil `worker`, damit er die laufende Studie nicht stört. F-017: lokal HTTPS mit Caddys eigenem Zertifikat, die Warnung wird weggeklickt (E-032).
- **Als Nächstes:** 5.1 Forwarded Headers.

## 2026-11-26 · Schritt 4.10 – Abnahme, Phase 4 abgeschlossen
- **Lokal mit echtem Postgres und Mailpit**, Migrationen 4 und 5 auf die Entwicklungsdatenbank angewendet. Admin-Passwort nur als Umgebungsvariable für den Testlauf, die User Secrets sind unverändert.
- **Durchlauf im Browser:** Anmeldung als Creator → Bestätigungsmail in Mailpit → Bestätigung → **Hinweis-Mail ans Team über echtes SMTP** (ohne Name und Adresse). Danach im Admin-Bereich: Weiterleitung zum Login, 5 Fehlversuche → Sperre (auch mit richtigem Passwort), nach Ablauf Login. Cookie ist `HttpOnly`, `Secure`, `SameSite=Strict` und ein Sitzungscookie. Filter landet in der URL (`/admin?kind=Creator`), der Suchtext nicht. CSV-Export mit BOM und passendem Dateinamen. Status, Datum und Notiz bearbeitet. Logout, danach wieder Login nötig. Test-Anmeldung über die Oberfläche gelöscht, ihre Mails sind aus dem Ausgang verschwunden.
- **Lighthouse:** alle Admin-Seiten und die Keep-Seite (DE/EN) mobil und Desktop 100/100/100. SEO-Abzug nur durch das gewollte `noindex`.
- **Gefunden und behoben:** „für 1 Minuten gesperrt“ heißt jetzt „für 1 Minute gesperrt“ (mit Test). Die Rückfrage-Seite zum Löschen war mittig statt linksbündig und hatte keinen Abstand unter dem Kasten.
- Für Phase 5 vorgemerkt: Die neuen Variablen kommen in die `.env.example`, und `hash-password` muss im Container laufen.
- Stand: 365 Tests grün, 5 Migrationen (`Initial`, `DataProtectionKeys`, `CleanupIndex`, `AdminPipeline`, `Retention`).
- **Als Nächstes:** Phase 5 (Docker und Compose). Vorher fein planen und deine Bestätigung abwarten.

## 2026-11-26 · Schritt 4.9 – Aufbewahrungsfrist (E-023)
- Bestätigte Anmeldungen: Nach 12 Monaten ohne Aktivität geht eine Warnmail raus (Sprache der Anmeldung, mit Löschdatum). Der Link „Ich möchte dabei bleiben“ führt zu `/{sprache}/signup/keep`, und erst der Button dort zählt. Danach kommt die Mail „Deine Anmeldung bleibt bestehen“. Ohne Klick wird 30 Tage nach der Warnmail gelöscht (E-031).
- Als Aktivität zählen die erste Bestätigung, der Klick und jede Änderung im Admin-Bereich. Ein erneut abgeschicktes Formular zählt nicht, auch nicht, wenn die Änderung bestätigt wird (F-013, F-015). Die Detailseite zeigt die letzte Aktivität und eine laufende Warnung.
- Fünfte Migration `Retention` mit drei Spalten und dem Nachtragen der letzten Aktivität für bestehende Daten (getestet wie in 4.3).
- `SignupDeletion` ist jetzt der eine Baustein zum Löschen, für den Admin-Bereich und für den Job.
- **Gefunden:** Lokalisierte Texte müssen innerhalb von `InCulture` gelesen werden. Im ersten Entwurf wären die Warnmails aus dem Hintergrund-Job in der Sprache des Servers gekommen. Außerdem braucht der Job `Site:BaseUrl`, weil es keine Anfrage gibt, aus der er den Link ableiten könnte. Deshalb steht es jetzt auch lokal und in Tests.
- Datenschutzerklärung (DE/EN) beschreibt die Frist. Die Checkliste hat einen neuen Punkt für die anwaltliche Prüfung der Mailtexte.
- 13 neue Tests (`RetentionTests`). Die Keep-Seite ist außerdem automatisch in den Seiten-, CSP- und Link-Tests (beide Sprachen).
- **Als Nächstes:** 4.10 Abnahme von Phase 4.

## 2026-11-26 · Schritt 4.8 – Benachrichtigung
- Bei der ersten Bestätigung einer Anmeldung geht eine Hinweis-Mail an `Notifications__Email`, über die Outbox und in derselben Transaktion wie die Bestätigung. Inhalt: Art, Sprache und der Link zur Detailseite, ohne Name und Adresse (E-030).
- Discord (`Notifications__DiscordWebhookUrl`) mit eigener Warteschlange und Hintergrunddienst, 3 Versuche. Ohne die Variable ist der Dienst gar nicht registriert. Beide Einstellungen werden beim Start geprüft.
- **Gefunden:** `IHttpClientFactory` schreibt jede URL ins Log, und damit stünde die geheime Webhook-URL im Log. Behoben mit `RemoveAllLoggers()`. Ich habe nachgeprüft, dass der Test ohne die Zeile wirklich fehlschlägt.
- 9 neue Tests (`NotificationTests`), mit einem nachgebauten Discord-Server und einem Log-Mitschnitt.
- **Als Nächstes:** 4.9 Aufbewahrungsfrist bestätigter Anmeldungen (E-023).

## 2026-11-26 · Schritt 4.7 – CSV-Export
- Button „CSV exportieren“ in der Liste. Er exportiert genau die gefilterte Liste (gleiche Filter, gleicher Suchtext) als `pubpot-anmeldungen-JJJJ-MM-TT.csv`.
- `SignupCsv`: UTF-8 mit BOM, Semikolon, CRLF, 27 Spalten. Formeln werden entschärft, Sonderzeichen in Anführungszeichen (E-029). `GET /admin/export` führt zurück zur Liste.
- **Neu gelernt:** `formaction` an einem Button schickt dasselbe Formular an ein anderes Ziel. Das Antiforgery-Token des Formulars geht mit.
- 16 neue Tests (`AdminExportTests`), darunter Formel-Namen wie `=HYPERLINK(…)`, Zeilenumbrüche und Anführungszeichen in Nachrichten.
- **Als Nächstes:** 4.8 Benachrichtigung bei neuer bestätigter Anmeldung.

## 2026-11-26 · Schritt 4.6 – Löschen (DSGVO)
- Auf der Detailseite gibt es „Anmeldung löschen …“. Das führt zur Rückfrage `/admin/signups/{id}/delete`, und erst „Endgültig löschen“ (POST) löscht. Danach geht es zurück zur Liste mit dem Hinweis „Die Anmeldung wurde gelöscht“.
- Gelöscht werden die Anmeldung, ihre Bestätigungslinks und alle Mails an diese Adresse, auch versendete. Gibt es unter derselben Adresse eine zweite Anmeldung (andere Rolle), bleiben die Mails erhalten, und die Rückfrage verlinkt die andere Anmeldung (E-028).
- Roter Button `btn-danger` in `admin.css`. Er nutzt die vorhandenen Farben `--danger` und `--on-primary`, damit der Kontrast in Hell und Dunkel passt.
- 7 neue Tests (`AdminDeleteTests`): Rückfrage löscht nichts, vollständiges Löschen inklusive Groß-/Kleinschreibung, zweite Rolle, Antiforgery, Login, unbekannte ID.
- **Als Nächstes:** 4.7 CSV-Export.

## 2026-11-26 · Schritt 4.5 – Detailseite
- `/admin/signups/{id}` zeigt alle Angaben: Kontakt, Entwickler- bzw. Creator-Felder, Nachricht (Zeilenumbrüche bleiben erhalten), Verlauf und Einwilligung. Hinweise für „noch nicht bestätigt“ und „Änderung wartet auf Bestätigung“.
- Formular für Status, Datum des nächsten Schritts und Notiz (höchstens 4000 Zeichen, nur intern). Speichern mit Post/Redirect/Get und dem Hinweis „Gespeichert“. Die Namen in der Liste verlinken auf die Detailseite, in der URL steht nur die ID.
- Speichern setzt `admin_updated_at` (zählt als Aktivität für E-023), aber **nicht** `updated_at`. Das bleibt die letzte Einreichung der Person, und danach richtet sich die Löschfrist unbestätigter Anmeldungen. Sonst würde jede Admin-Notiz eine unbestätigte Anmeldung verlängern.
- Der Kanal eines Creators wird bewusst nur als Text gezeigt, nicht als Link: Er ist freie Eingabe (z. B. könnte dort ein `javascript:`-Link stehen). Der Steam-Link wird aus der App-ID gebaut und ist deshalb sicher.
- **Gefunden:** Im Namespace `PubPot.Web.Pages.Admin.Signups` ist `Signup` doppeldeutig (auch Namespace der öffentlichen Seiten), daher `Data.Signup`.
- 13 neue Tests (`AdminDetailTests`).
- **Als Nächstes:** 4.6 Löschen (DSGVO).

## 2026-11-26 · Schritt 4.4 – Liste mit Filter und Suche
- `/admin` zeigt alle Anmeldungen als Tabelle: Eingang, Name mit E-Mail, Art, Studio/Spiel bzw. Kanal, Sprache, Bestätigung, Status, nächster Schritt. Neueste zuerst, 50 pro Seite.
- Filter nach Art, bestätigt, Status und Sprache in der URL. Der Suchtext geht nur per POST und landet so nie in URLs oder Logs (E-027).
- `AdminSignups` (Abfragen) und `AdminText` (deutsche Bezeichnungen, Datum in Europe/Berlin, gespeichert wird UTC).
- **Gefunden:** `page` ist in Razor Pages ein reservierter Routenwert. Als Name für die Seitennummer führte er zu einem Fehler 500 bei der Weiterleitung. Jetzt heißt der Parameter `p`.
- Die Login-Tests laufen jetzt gegen die Test-Datenbank, weil `/admin` Daten lädt.
- 23 neue Tests (`AdminListTests`): jeder Filter, Suche in allen Feldern, `%`/`_` als normale Zeichen, kein Suchtext in URLs, Seiten, HTML-Ausgabe maskiert (XSS).
- **Als Nächstes:** 4.5 Detailseite mit Bearbeiten.

## 2026-11-26 · Schritt 4.3 – Datenmodell Pipeline
- `Signup` hat neue Felder: `PipelineStatus` (New, Contacted, Conversation, Pilot, Declined, als Text gespeichert), `AdminNote` (höchstens 4000 Zeichen), `NextStepOn` (Datum) und `AdminUpdatedAt` (letzte Änderung im Admin-Bereich, zählt später für E-023).
- Vierte Migration `AdminPipeline`. Die Spalte `pipeline_status` ist Pflicht. Den Standardwert für bestehende Zeilen habe ich von Hand von `""` auf `"New"` geändert, sonst hätten alte Anmeldungen einen ungültigen Status.
- **Neu gelernt:** Eine Migration lässt sich mit Daten testen: eine eigene Datenbank nur bis zur vorigen Migration aufbauen (`IMigrator.MigrateAsync("CleanupIndex")`), eine alte Zeile einfügen, dann bis zum Ende migrieren und das Ergebnis prüfen.
- Kein eigener Index für den Status: Bei ein paar hundert Anmeldungen ist ein Durchsuchen der Tabelle schneller als jeder Index.
- 3 neue Tests (`AdminPipelineModelTests`) und Beispieldaten `TestData` für die nächsten Schritte.
- **Als Nächstes:** 4.4 Liste mit Filter und Suche.

## 2026-11-26 · Schritt 4.2 – Login, Logout, Sperre
- Cookie-Login für `/admin` (`__Host-pubpot-admin`, Secure, HttpOnly, SameSite=Strict, Sitzungscookie), 8 Stunden ohne Aktivität, höchstens 24 Stunden insgesamt. Begründungen in E-026.
- Sperre nach 5 Fehlversuchen für 15 Minuten (`AdminLoginThrottle`), Rate-Limit `admin-login` mit 10 Versuchen pro IP in 10 Minuten. Benutzername und Passwort werden in gleich langer Zeit geprüft.
- Abmelden nur per POST. Die Sitzung wird zusätzlich gesperrt, sodass ein kopiertes Cookie nicht mehr gilt. Ein Passwortwechsel beendet alle Sitzungen.
- Ohne `Admin__PasswordHash` antwortet alles unter `/admin` mit 404. Admin-Seiten sind immer Deutsch, ohne Sprachpräfix, mit `no-store` und `noindex`.
- Eigenes Layout und `admin.css`, das nur im Admin-Bereich geladen wird. Die öffentlichen Seiten bleiben dadurch gleich schlank.
- **Gefunden:** Ein `<form action="/admin/logout">` bekommt kein Antiforgery-Token, weil ASP.NET es nur bei `asp-page`/`asp-action` einfügt. Damit kein Token an fremde Adressen geht.
- 24 neue Tests (`AdminLoginTests`), darunter ein Test, der alle Admin-Seiten selbst findet und den Login-Schutz prüft. Neue Test-Hilfen: `TestClock` (Uhr zum Vorstellen) und `LoginAsAdminAsync`.
- **Als Nächstes:** 4.3 Datenmodell für die Pipeline.

## 2026-11-26 · Schritt 4.1 – Admin-Konfiguration und Hash-Befehl
- `AdminOptions` (`Admin__Username`, Standard `admin`, und `Admin__PasswordHash`). Ohne Hash ist der Admin-Bereich aus, dazu gibt es eine Warnung im Log. Ein ungültiger Hash oder ein leerer Benutzername verhindern den Start.
- `AdminPassword`: Hashen und Prüfen mit dem `PasswordHasher` aus ASP.NET Core Identity (PBKDF2/HMAC-SHA512, 100 000 Runden). Ein kaputter Hash ergibt `false` statt einer Ausnahme.
- Befehl `dotnet run --project src/PubPot.Web -- hash-password`: Im Terminal wird das Passwort verdeckt und zweimal abgefragt (mindestens 12 Zeichen), dann kommen die fertigen Zeilen für `.env` und User Secrets. Über eine Pipe wird nur der Hash ausgegeben. Begründung in E-025.
- README: Abschnitt „Admin-Zugang“ mit den neuen Einstellungen.
- 14 neue Tests (`AdminPasswordTests`): Hash, Salt, kaputte Hashes, Befehl im Terminal und über eine Pipe, Start mit und ohne gültige Einstellungen.
- **Als Nächstes:** 4.2 Login, Logout und Sperre.

## 2026-11-26 · Phase 4 geplant, F-014 und F-015 beantwortet
- Phase 4 in zehn Schritte aufgeteilt (4.1 bis 4.10), siehe `ROADMAP.md`. Den Plan hast du freigegeben.
- **F-014:** Der Admin-Bereich ist nur auf Deutsch (E-024).
- **F-015:** Nur eine Änderung im Admin-Bereich zählt als Aktivität, auch „Pilot“ bekommt also nach 12 Monaten die Warnmail (Ergänzung zu E-023).
- **Als Nächstes:** 4.1 Admin-Konfiguration und Hash-Befehl.

## 2026-11-26 · Startdatei für neue Sitzungen
- Neue Datei `START-SESSION.md` im Repo-Wurzelordner: Überblick, Regeln, Stand, nächste Aufgabe, Wegweiser zu den Dateien. Zu Beginn einer Sitzung wird nur sie gelesen, das hält den Kontext klein.
- `CLAUDE.md` angepasst: Sitzungsstart über `START-SESSION.md`, die am Ende jeder Sitzung aktualisiert wird.
- **Als Nächstes:** Phase 4 (Admin-Bereich), erst nach deiner Bestätigung.

## 2026-11-26 · F-013 beantwortet
- Bestätigte Anmeldungen: 12 Monate ohne Aktivität → Warnmail mit Link „Ich möchte dabei bleiben“ → ohne Klick nach 30 Tagen gelöscht. Mit Klick gibt es eine Bestätigungsmail, und die Frist beginnt neu. Aktiver Admin-Status zählt ebenfalls als Aktivität (E-023).
- Wird nach Phase 4 gebaut. In der Roadmap vermerkt.
- **Phase 4 startet erst nach deiner Bestätigung.**

## 2026-11-26 · Schritt 3.9 – Abnahme, Phase 3 abgeschlossen
- **Release-Build im Production-Modus** mit Konfiguration nur über Umgebungsvariablen (`ConnectionStrings__Default`, `Site__BaseUrl`, `Smtp__*`). Das ist eine Generalprobe für Docker in Phase 5.
- **Durchlauf auf Englisch:** `/` → `/en` (Browsersprache) → Creator-Formular → englische Mail in Mailpit mit Link aus `Site:BaseUrl` → bestätigt.
- **Lighthouse** (Release): `/de/signup/developer`, `/en/signup/creator`, `/de` jeweils mobil und Desktop **100/100/100/100**.
- **Gefunden und behoben:** Der Sprachumschalter hatte ein `aria-label`, das den sichtbaren Text „EN“ nicht enthielt (WCAG 2.5.3 „Label in Name“, Sprachsteuerung findet den Link sonst nicht). Jetzt sichtbarer Text plus versteckter Zusatz.
- Datenschutz-Fakten ergänzt: Antiforgery-Cookie, IP nur im Arbeitsspeicher, Aufbewahrungsfristen. Neue Rückfrage F-013 (Frist für bestätigte Anmeldungen).
- Erwartete DB-Fehlermeldungen im Testprotokoll stummgeschaltet (CI-Ausgabe bleibt lesbar).
- Stand: 234 Tests grün, 3 Migrationen (`Initial`, `DataProtectionKeys`, `CleanupIndex`).
- **Als Nächstes:** Phase 4 (Admin-Bereich). Vorher fein planen.

## 2026-11-26 · Schritt 3.8 – Aufräum-Job
- `DataCleanup` löscht direkt in der Datenbank (`ExecuteDelete`, ohne Daten zu laden): unbestätigte Anmeldungen 30 Tage nach der **letzten** Einreichung (passend zum Link aus der letzten Mail), abgelaufene Links, versendete Mails nach 7 Tagen, endgültig gescheiterte Mails nach 30 Tagen. Läuft stündlich als Hintergrunddienst, erstmals 30 s nach dem Start.
- Dritte Migration `CleanupIndex`: Der Index für den Job liegt jetzt auf `(confirmed_at, updated_at)` statt `created_at`.
- Neues Prüfskript `scripts/check.sh` (Build im CI-Modus, Format, Tests, Abbruch beim ersten Fehler), angelegt als Folge des Fehlers in 3.7.
- Tests prüfen vor allem, was **nicht** gelöscht werden darf: bestätigte Anmeldungen, frische Unbestätigte, Mails, die noch versucht werden. 234 Tests grün.

## 2026-11-26 · Schritt 3.7 – Spam-Schutz
- Honeypot, Mindestausfüllzeit (signierter Zeitstempel), Rate-Limiting für POST pro IP, Sperrfrist gegen Mail-Fluten, Antiforgery (war schon aktiv). Gemeinsame Basisklasse `SignupFormPageModel` für beide Formulare (E-022).
- Data-Protection-Schlüssel in der Datenbank, zweite Migration `DataProtectionKeys`.
- Neue Statusseite `/status/{code}` (404, 429, sonstige Fehler) ersetzt `/not-found`.
- Tests: 230 grün. Die normalen Tests entschärfen den Spam-Schutz per Einstellung, `SpamProtectionTests` schalten ihn gezielt scharf.
- Im echten Browser gegengeprüft: sofort abgeschickt → still verworfen, keine Mail. Mit 4 s Pause → Mail kommt in Mailpit an. Honeypot liegt bei x = −10000.
- Stolperstein: Python-Hilfsskripte mit `"""` scheitern an C#-Code, der selbst `"""` enthält. Abhilfe: Skript als Datei mit `'''`-Strings.
- **Fehler im Ablauf, korrigiert:** Commit `f150b3f` wurde trotz Fehler von `dotnet format` (überflüssige `using`-Direktive in `RateLimits.cs`) erstellt und gepusht, weil die Befehle mit `;` statt `&&` verkettet waren. In der CI wäre er rot gewesen. Folge-Commit behebt das. Lehre: Prüfschritte vor Commits immer mit `&&` verketten und die Exit-Codes prüfen, nicht nur die Ausgabe filtern.

## 2026-11-26 · Schritt 3.6 – Double-Opt-in
- Absenden → Anmeldung, Bestätigungslink (nur Hash gespeichert, 30 Tage gültig) und Mail im Ausgang, alles in einer Transaktion. Ältere Links derselben Anmeldung verfallen.
- Änderungen an bestätigten Anmeldungen warten als JSON am Link und werden erst beim Bestätigen übernommen.
- Bestätigungsseite: GET prüft nur und zeigt den Button, erst der POST bestätigt (Schutz vor Mail-Scannern). Danach Weiterleitung auf `?status=…`.
- Hintergrunddienst verschickt per SMTP (MailKit) mit Wiederholungen (E-021). In Tests ersetzt ein Fake den Versand.
- `LinkBuilder`: absolute Links aus `Site:BaseUrl`, in Production Pflicht (Schutz vor Host-Header-Injection, per Test abgesichert).
- **Ende-zu-Ende mit Mailpit geprüft:** Formular im Browser → Mail nach einem Versuch in Mailpit → Link → Button → bestätigt, Link verbraucht.
- **Dabei gefunden (Fehler aus Phase 2):** Die Schriften wurden doppelt geladen. Die Preload-Links bekamen den Fingerprint-Namen, das CSS lädt aber den normalen Namen. Behoben, dazu ein Test, der Preload-URLs und CSS-URLs vergleicht. Lighthouse hatte das nicht bemängelt, die Browserkonsole schon.
- Stolperstein im Test: Antiforgery-Tokens gelten seitenübergreifend. Der Test-Helfer kann sie jetzt von einer anderen Seite holen, wenn die Zielseite kein Formular zeigt (z. B. bei einem ungültigen Link).
- 221 Tests grün.

## 2026-11-26 · Schritt 3.5 – Creator-Formular
- Felder: Name, E-Mail, Plattformen (Checkboxen → Bitmaske), Kanal-Link oder Twitch-Login (`ChannelInput`: http(s)-Link mit Domain oder 4–25 Zeichen `[A-Za-z0-9_]`), Zuschauer-Bereich (5 Stufen), Lieblingsgenres, 18+-Bestätigung, Nachricht, Einwilligung.
- Dieselbe E-Mail als Entwickler und als Creator ergibt zwei Einträge (E-020), per Test abgesichert.
- Ausgewählte Plattformen bleiben nach einem Validierungsfehler angehakt.
- 207 Tests grün. Im Browser geprüft (dunkel, mobil, mit Fehlern).

## 2026-11-26 · Schritt 3.4 – Entwickler-Formular
- Aufbau: `Signups/` (Geschäftslogik: `DeveloperSubmission`, `SignupService` mit Zusammenführen, `SteamLink`, `ReleaseChoice`, `ConsentText`) getrennt von der Razor Page. Post/Redirect/Get zur Danke-Seite.
- Validierung: Der Browser prüft vorab ohne JavaScript, maßgeblich ist der Server. Meldungen und Feldnamen sind übersetzt (`Validation.*`, `Form.*` in SharedResource). Eigene Tag-Helper setzen `aria-invalid` und verknüpfen die Fehlermeldung mit dem Feld.
- Neue Farb-Tokens `--input-border` (Feldrahmen ≥ 3:1, WCAG 1.4.11) und `--danger`, beide im Kontrast-Test.
- Einwilligung: Version `2026-11-26` wird mit jeder Anmeldung gespeichert. Ein Test vergleicht den Hash des Textes (DE+EN) mit der Version. Wer den Text ändert, ohne die Version zu erhöhen, bekommt einen roten Test.
- **Im Browser gefunden und behoben:**
  1. `type="url"` beim Steam-Link hat Links ohne `https://` im Browser blockiert, obwohl der Server sie akzeptiert. Jetzt `type="text"` mit `inputmode="url"`.
  2. Eigene Validierungsattribute zeigten ihren Schlüssel („Validation.Consent“) statt des Textes. ASP.NET übersetzt nur Attribute mit „Adapter“, deshalb gibt es jetzt `LocalizedValidationAdapterProvider`.
- **Lernpunkt:** Ein komplett leerer POST (nur von Bots) bindet `Input` gar nicht. Dann gibt es keine Feldfehler, aber auch keine Speicherung. Der Test bildet jetzt echtes Browser-Verhalten nach, der Bot-Fall ist separat abgesichert.
- Tests: 184 grün (neu: Formular gegen echte DB inkl. Zusammenführen, Sprachwahl, ungültige Werte, fehlender Antiforgery-Token → 400; Regeln für Steam-Link, E-Mail, Release, Einwilligungs-Hash).

## 2026-11-26 · Schritt 3.3 – Readiness und Tests mit echter Datenbank
- `/health` ist reine Liveness (keine Prüfungen). `/health/ready` prüft, dass die Datenbank erreichbar ist **und** keine Migration fehlt (`MigrationsHealthCheck`). Der Smoke-Test nach dem Deploy erkennt so auch eine vergessene Migration.
- Testcontainers 4.17.0: `DatabaseWebFactory` startet `postgres:16.16` einmal pro Testlauf und legt das Schema über die echten Migrationen an. `ResetAsync()` leert die Tabellen zwischen Tests.
- Neue Tests: bereit mit migrierter DB (200), nicht bereit ohne Migrationen (503), nicht bereit, aber lebendig ohne DB (503/200). 135 Tests grün in ca. 9 s.
- Lokal gegen die Compose-Datenbank geprüft: `/health` und `/health/ready` liefern 200.

## 2026-11-26 · Schritt 3.2 – EF Core und Datenmodell
- Pakete: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.4, EFCore.NamingConventions 10.0.1 (snake_case), EF Design 10.0.15 (nur für `dotnet ef`, nicht in der veröffentlichten App).
- `dotnet-ef` 10.0.15 als lokales Tool (`dotnet-tools.json`, ab SDK 10 im Wurzelordner statt `.config/`).
- Modell: `signups` (Entwickler und Creator in einer Tabelle, Spalte `kind`, eindeutig pro Rolle und E-Mail), `signup_confirmations` (nur Token-Hash, Änderungen als `jsonb`), `outbox_emails` (Mail-Ausgang). IDs sind UUIDv7 (zeitlich sortierbar, nicht erratbar).
- Die App bricht ohne `ConnectionStrings:Default` sofort ab (fail fast). Lokal liegt die Verbindung in den User Secrets, die Tests verwenden eine Factory mit Schein-Verbindung.
- Migration `Initial` erzeugt und lokal angewendet, Tabellen mit `\d signups` geprüft.

## 2026-11-26 · Schritt 3.1 – Hilfsdienste in Docker
- `deploy/compose.yaml` (Basis für alle Umgebungen, Projektname `pubpot`, Postgres ohne offene Ports) und `deploy/compose.override.yaml` (nur lokal: Postgres, Mailpit und Adminer auf `127.0.0.1`). Images mit fester Version: `postgres:16.16`, `axllent/mailpit:v1.34.0`, `adminer:5.5.1`.
- Init-Skript legt `pubpot` und `twitch` mit eigenen Benutzern an, `REVOKE ALL … FROM PUBLIC` sperrt den Zugriff über Kreuz.
- Lokale `deploy/.env` mit Zufallspasswörtern erzeugt. Sie ist nicht im Repo (`git check-ignore` geprüft).
- Adminer auf Port 8081, weil 8080 vom Adminer der laufenden Studie belegt ist. Kein Konflikt mit der Studie, eigene Volumes (`pubpot_pgdata`).
- Geprüft: Rechtetrennung (pubpot ↔ twitch gesperrt), Passwortprüfung über das Docker-Netz, Mailpit und Adminer erreichbar.
- **Lernpunkt:** Innerhalb des Postgres-Containers gilt für `localhost` `trust`, also kein Passwort. Ein Test **im** Container sagt deshalb nichts über die Passwortprüfung aus. Richtig testet man so, wie die App sich verbindet: aus einem anderen Container über das Netzwerk.

## 2026-11-26 · Schritt 2.6 – Tests, Phase 2 abgeschlossen
- 132 Tests in 5 Klassen: `PageTests` (alle Seiten, kein Roh-Schlüssel, Crawler über alle internen Links), `LocalizationTests` (Weiterleitung, Umschalter, Open Redirect, 404, hreflang, noindex, Vorschlag), `SecurityTests` (Header auch auf CSS-Dateien, kein Inline-JS/-CSS, keine fremden Hosts, HSTS nur über HTTPS in Production, `/health`), `ResourceTests` (DE/EN-Parität, keine Steuerzeichen, jeder View-Schlüssel existiert), `ContrastTests` (WCAG AA hell und dunkel aus `site.css`).
- Gegenprobe mit zwei absichtlichen Fehlern: Beide wurden erkannt (E-019).
- Der Build läuft auch mit `CI=true` (Warnungen = Fehler) sauber.
- Platzhalter-Test aus 1.4 entfernt.
- **Als Nächstes:** Phase 3 (Formulare, Datenbank, Double-Opt-in). Vorher fein planen.

## 2026-11-26 · Schritt 2.5 – Security-Header und /health
- Security-Header-Middleware mit strikter CSP, HSTS (nur außerhalb von Development und nur bei HTTPS), Cookie-Policy, kein `Server`-Header (E-018).
- `/health` (Liveness) über ASP.NET HealthChecks. `/health/ready` mit Datenbank folgt in Phase 3.
- Geprüft: Header mit curl, im Browser keine CSP-Verletzung auf Startseite, Rechtsseiten, Danke- und 404-Seite. Schrift und Theme-Skript laden trotz CSP.

## 2026-11-26 · Schritt 2.4 – Startseite
- Abschnitte: Hero mit zwei Zielgruppen-Karten, „So funktioniert's“ (3 Schritte), Rechenbeispiel (als „Beispiel – keine Prognose“ markiert), Für Entwickler, Für Creator, Pilot mit Next-Fest-Termin, FAQ (9 Fragen, `<details>` ohne JavaScript), Abschluss-CTA.
- Texte aus dem Auftrag abgeleitet, ohne Einnahmeversprechen („Rechne mit einem kleinen Zuverdienst, nicht mit einem Einkommen“). Bitte inhaltlich prüfen → F-012.
- **Lighthouse (Release-Build, `/de`):** mobil 99/100/100/100, Desktop 100/100/100/100 (Performance/Accessibility/Best Practices/SEO).
- Kontraste zusätzlich selbst berechnet, weil Lighthouse nur den hellen Modus prüft. Dabei gefunden: Der Akzentton im hellen Modus hatte in grauen Abschnitten 4,36:1 (< 4,5). Er ist jetzt dunkler (`#92400e`, 6,16:1).
- Stolperstein: Ein eigenes Ersetzungsskript (geschützte Leerzeichen vor % und €) hat durch einen Escaping-Fehler Steuerzeichen in die Texte geschrieben. Der Build hat die ungültige `.resx` sofort abgelehnt, die sieben Stellen sind repariert und geprüft.

## 2026-11-26 · Schritt 2.3 – Restliche Seiten
- Reihenfolge mit 2.4 getauscht, damit die Links der Startseite sofort funktionieren.
- Neue Seiten jeweils unter `/de` und `/en`: `signup/developer`, `signup/creator`, `signup/thanks`, `signup/confirm`, `legal/imprint`, `legal/privacy`. URLs werden klein geschrieben (`LowercaseUrls`).
- Impressum und Datenschutz sind Platzhalter mit TODO und „anwaltlich prüfen lassen“, dazu `noindex`. Die Datenschutzseite listet **technische Fakten** als Arbeitsgrundlage für den Anwalt, ausdrücklich als „kein Rechtstext“ gekennzeichnet. Diese Liste wächst mit dem Projekt mit.
- Danke- und Bestätigungsseite: `noindex`. Die Logik (Token prüfen) folgt in Phase 3.
- `robots.txt`: alles erlaubt außer `/lang/`.
- Geprüft: Alle 14 Seiten liefern 200, kein Ressourcen-Schlüssel erscheint als Rohtext, kein Konsolenfehler.

## 2026-11-26 · Schritt 2.2 – Deutsch und Englisch
- Sprachpräfix per Konvention, Weiterleitung von `/`, Umschalter mit Cookie, Sprachvorschlag, hreflang/canonical, lokalisierte Fehler- und 404-Seiten (E-017).
- Mit curl geprüft: Weiterleitung für de/en/fr/leer, Cookie hat Vorrang, `/fr` und `/DE` → 404, Open Redirect über `returnPath=//evil.com` blockiert.
- Im Browser geprüft: Vorschlagsleiste → Klick → `/en` ohne Leiste → zurück auf `/de` ohne Leiste.
- **Für die Datenschutzerklärung notiert:** Cookie `pubpot_lang` (nur nach Klick, 1 Jahr), `localStorage` `pubpot-theme` (nur im Browser, wird nicht übertragen).

## 2026-11-26 · Schritt 2.1 – Web-Projekt und Design-Grundlage
- `src/PubPot.Web` von Hand angelegt (ohne Bootstrap/jQuery aus der Vorlage, E-015). Layout mit Kopfzeile, Wortmarke „Pub**Pot**“, Navigation (ab 960 px sichtbar), Hell/Dunkel-Schalter und Fußzeile.
- Schriften Inter und Space Grotesk (Fontsource 5.3.0, Latin-Teilmenge mit Umlauten und €) selbst gehostet.
- Im Browser geprüft (Screenshots hell/dunkel/mobil): Schriften laden, keine Konsolenfehler, auf 390 px Breite kein seitlicher Scroll.
- Stolperstein: Beim ersten Test fehlten CSS und JS. Ursache war der Startbefehl (`dotnet run --environment`), siehe E-016.
- Texte sind noch fest auf Deutsch, die Lokalisierung folgt in 2.2.

## 2026-11-26 · Rückfragen F-006 bis F-010 beantwortet
- F-006: getrennte Datenbanken für Web und Worker (E-011). F-007: kein externer Check (E-012). F-010: nur E-Mail (E-014).
- F-008/F-009: Einen VPS gibt es noch nicht. Erst läuft alles lokal, Monitoring, Alarme und Backups eingeschlossen, danach kommt der VPS. Die Phasen 7 und 8 sind in der Roadmap getauscht (E-013).
- `RUECKFRAGEN.md` neu gegliedert in „Offen“ und „Beantwortet“. Dabei einen Fehler von mir behoben: Der ursprüngliche Text von F-004 war unter F-011 gerutscht.
- Zurzeit sind keine Fragen offen.
- **Als Nächstes:** Phase 2, sobald du OK gibst.

## 2026-11-26 · Erster Push nach GitHub
- `gh` mit Fine-grained-Token angemeldet. Der Token hat nur Zugriff auf `pubpot` (Least Privilege).
- Vor dem Push die gesamte Historie geprüft: keine Twitch-Zugangsdaten, kein altes DB-Passwort, keine echte E-Mail-Adresse, keine `.env`, keine `AUFTRAG.md`.
- Stolperstein: `git push` scheiterte mit `unable to get local issuer certificate`. Git nutzte OpenSSL mit eigener Zertifikatsliste, `gh` dagegen den Windows-Speicher. Lösung: `git config http.sslBackend schannel`, nur für dieses Repo. Die Zertifikate werden weiter geprüft, nur eben gegen den Windows-Speicher.
- Repo: https://github.com/TidragoUnity/pubpot

## 2026-11-26 · Schritt 1.4 – Testprojekt, Phase 1 abgeschlossen
- `tests/PubPot.Web.Tests` mit xUnit v3 und einem Platzhalter-Test. `dotnet test --solution PubPot.slnx` ist grün.
- Stolperstein: Die Vorlage nutzt noch xUnit v2/VSTest. Mit xUnit v3 verlangt SDK 10+ die Microsoft Testing Platform, die jetzt in `global.json` eingeschaltet ist (E-010).
- **Als Nächstes:** Phase 2, Schritt 2.1 (Web-Projekt, Layout, Design-Grundlage).

## 2026-11-26 · Schritt 1.3 – Worker auf .NET 10
- Ziel `net10.0`, Npgsql 10.0.4, Microsoft.Extensions.Hosting 10.0.15. Der Code brauchte keine Änderungen für die neuen Versionen.
- Dockerfile baut jetzt aus dem Repo-Wurzelordner (wegen `global.json` und `Directory.*.props`), Build-Image `sdk:11.0`, Laufzeit-Image `runtime:10.0`. Neue `.dockerignore` im Wurzelordner.
- Test: Image gebaut und gegen eine temporäre Postgres-16-Instanz laufen lassen, mit Fake-Zugangsdaten, damit die laufende Studie nicht beeinträchtigt wird. Ergebnis: Schema angelegt, Runden gespeichert (`complete = false`, wie erwartet), Prozess läuft als Nutzer `app`.
- Gefunden und behoben: Npgsql ab Version 9 versucht, `libgssapi_krb5` zu laden, und schreibt einen irreführenden Fehler ins Log. GSS-Verschlüsselung ist jetzt im Code abgeschaltet, weil wir kein Kerberos nutzen.
- Offen für später: Ein echter Lauf mit Twitch-Zugangsdaten, sinnvoll nach dem Ende der Studie.

## 2026-11-26 · Schritt 1.2 – Grundgerüst
- `PubPot.slnx` (neues XML-Solution-Format), `global.json` auf SDK 11.0.100, zentrale Paketverwaltung, `.editorconfig`, `.gitattributes` (E-009).
- Der Worker nutzt die zentralen Versionen (Npgsql steht fest auf 8.0.9 statt `8.0.*`, weil Wildcards bei zentraler Verwaltung nicht erlaubt sind).
- `dotnet build` ohne Warnungen, `dotnet format --verify-no-changes` sauber.

## 2026-11-26 · Schritt 1.1 – Worker übernommen
- Quellcode aus `C:\Timo\Docker\twitch-worker` nach `src/TwitchWorker` kopiert. Das Original und der laufende Container bleiben unberührt.
- Vor dem Commit geprüft: Die Werte aus der alten `.env.example` stehen in keiner Datei im Repo. Die neue `.env.example` enthält nur Platzhalter.
- Build mit SDK 11 (Ziel noch `net8.0`) läuft grün.

## 2026-11-26 · F-004 und F-011 beantwortet
- F-004: Variante A, also Ziel `net10.0`, gebaut mit SDK 11 (E-007).
- F-011: Anonyme Commit-Adresse, `AUFTRAG.md` nur lokal. Die Historie wurde vor dem ersten Push umgeschrieben (E-008).
- Plan ist freigegeben → Phase 1 beginnt.

## 2026-11-26 · Antworten F-001 bis F-005 eingearbeitet
- F-001: Worker kommt als eigenes Projekt ins Repo. F-002: `.env` bleibt draußen. F-003: öffentliches Repo unter deinem Konto. F-005 ist damit erledigt (echte Freigabe möglich).
- F-004 (SDK 11 statt 10): Optionen erklärt, Entscheidung steht aus. Die .NET-10-Laufzeit ist installiert, es fehlt nur das SDK.
- Neu: F-011 (GitHub-Zugang, was ein öffentliches Repo bedeutet).
- **Als Nächstes:** Antwort zu F-004, dann Schritt 1.1.

## 2026-11-26 · Phase 0
**Erledigt**
- Auftrag gelesen (Kopie in [AUFTRAG.md](AUFTRAG.md)), bestehenden Worker angesehen.
- Git-Repo `C:\Timo\Docker\pubpot` angelegt, Plan-Dateien erstellt.

**Beobachtungen**
- Der Worker läuft gerade (Marktstudie, 168 h). Er bleibt unangetastet → F-001.
- `twitch-worker/.env.example` enthält vermutlich echte Zugangsdaten → F-002.
- Lokal ist kein .NET-10-SDK installiert (nur 9 und 11) → F-004.

**Als Nächstes**
- Warten auf dein OK zum Plan ([ROADMAP.md](ROADMAP.md)) und auf die Antworten zu F-001 bis F-004.
- Danach Schritt 1.1.
