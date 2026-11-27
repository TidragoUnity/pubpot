# Entscheidungen

Kurzes Entscheidungslog (ADR-light). Jeder Eintrag beantwortet: **Was** wurde entschieden, **warum**, und **welche Alternative** gab es?
Das ist auch dein Lernmaterial: Hier steht, warum etwas so gebaut ist.

---

## E-001 · Fortschritt in Markdown-Dateien im Repo
**Entscheidung:** Status in `ROADMAP.md`, Verlauf in `JOURNAL.md`, Fragen in `RUECKFRAGEN.md`, Begründungen hier.
**Warum:** Alles liegt versioniert neben dem Code, jede Änderung ist in der Git-Historie sichtbar, und es braucht kein extra Werkzeug.
**Alternative:** GitHub Issues/Projects. Das ist sinnvoll, sobald das Repo auf GitHub liegt und mehrere Leute mitarbeiten.

## E-002 · Ein Commit pro Roadmap-Schritt, Nachricht beginnt mit der Schrittnummer
**Warum:** Kleine, lauffähige Commits lassen sich einzeln prüfen und zurückdrehen (`git revert`), und `git log` passt direkt zur Roadmap.

## E-003 · Repo getrennt vom laufenden Worker
**Entscheidung:** Neues Repo unter `C:\Timo\Docker\pubpot`, der Worker-Code wird hineinkopiert, nicht verschoben.
**Warum:** Docker Compose leitet den Projektnamen und damit den Volume-Namen (`twitch-worker_pgdata`) aus dem Ordnernamen ab. Ein Verschieben könnte die laufende Studie stören. Bestätigt durch F-001: Der Worker wird eigenes Projekt `src/TwitchWorker`.

## E-004 · Öffentliches GitHub-Repo
**Entscheidung (F-003):** Das Repo ist öffentlich, auch als Showcase.
**Folgen:** GitHub Actions ohne Minutenlimit, Environment-Freigaben (Required reviewers) und Secret Scanning sind kostenlos. Dafür gilt: Was gepusht ist, ist öffentlich und bleibt in der Historie. Secrets liegen nur in `.env` (per `.gitignore` ausgeschlossen) und in den GitHub-Secrets.

## E-005 · Production-Freigabe über GitHub Environment
**Entscheidung (F-005):** Production-Deploy bei Tag `v*`, mit manueller Freigabe im Environment `production`, wie im Auftrag. Möglich, weil das Repo öffentlich ist (E-004).
**Alternative (verworfen):** Nur ein per Hand gestarteter Workflow (`workflow_dispatch`). Er wäre bei einem privaten Repo ohne kostenpflichtigen GitHub-Plan nötig gewesen.

## E-006 · Keine Zugangsschlüssel im Chat
**Entscheidung:** Der Zugang zu GitHub läuft über `gh auth login`, das du selbst ausführst. Tokens werden nie in den Chat kopiert.
**Warum:** Was im Chat steht, landet im Verlauf. Ein Token, das dort liegt, gilt als kompromittiert.

## E-007 · Ziel `net10.0`, gebaut mit SDK 11
**Entscheidung (F-004, Variante A):** Alle Projekte zielen auf `net10.0` (LTS). Gebaut wird lokal, in der CI und im Docker-Build-Image mit SDK 11. Die Version legt `global.json` fest. Die Laufzeit-Images sind `aspnet:10.0` und `runtime:10.0`.
**Warum:** Das **SDK** ist das Werkzeug zum Bauen, die **Laufzeit** führt das Programm aus. Ein neueres SDK baut problemlos für eine ältere Zielversion. Wichtig ist nur, dass überall dasselbe SDK verwendet wird, sonst verhalten sich `dotnet format` und die Analyzer lokal anders als in der CI.
**Folge:** Dependabot darf Microsoft-, EF-Core- und Npgsql-Pakete nicht auf 11.x heben. Dafür kommt in Phase 6 eine Ignore-Regel.

## E-009 · Gemeinsame Build-Einstellungen im Repo-Wurzelordner
**Entscheidung:** `Directory.Build.props` (Einstellungen für alle Projekte), `Directory.Packages.props` (alle Paketversionen an einer Stelle), `.editorconfig` (Code-Stil), `.gitattributes` (Zeilenenden LF), `global.json` (SDK-Version).
**Warum:** Jedes neue Projekt (Web, Tests) übernimmt die Regeln automatisch. In der CI sind Warnungen Fehler (`CI=true`), lokal nicht. Mit LF-Zeilenenden laufen Shell-Skripte im Linux-Container, mit CRLF aus Windows brechen sie ab (`/bin/sh^M: not found`).
**Alternative:** Einstellungen pro `.csproj`. Das wird schnell uneinheitlich.

## E-010 · xUnit v3 auf der Microsoft Testing Platform
**Entscheidung:** Für Tests nutzen wir `xunit.v3`, ausgeführt über die Microsoft Testing Platform (MTP). Eingeschaltet wird sie in `global.json` unter `"test": { "runner": "Microsoft.Testing.Platform" }`.
**Warum:** xUnit v2 (das noch in der `dotnet new`-Vorlage steckt) wird nur noch gewartet. xUnit v3 setzt auf MTP, und ab SDK 10 lehnt `dotnet test` den alten VSTest-Weg für solche Projekte ab. Mit MTP braucht es nur noch ein einziges Paket.
**Achtung:** Mit MTP lautet der Aufruf `dotnet test --solution PubPot.slnx` statt `dotnet test PubPot.slnx`. Testabdeckung kommt bei Bedarf in Phase 6 über `Microsoft.Testing.Extensions.CodeCoverage`.

## E-011 · Getrennte Datenbanken für Web und Worker
**Entscheidung (F-006):** Eine Postgres-Instanz mit zwei Datenbanken, `pubpot` (Web) und `twitch` (Worker). Jede hat einen eigenen DB-Benutzer, der nur auf seine Datenbank zugreifen darf. Die Web-App migriert per EF-Bundle. Der Worker bekommt ein versioniertes SQL-Skript statt `CREATE TABLE` im Code.
**Warum:** Prinzip der geringsten Rechte. Eine Sicherheitslücke in der Web-App kommt nicht an die Worker-Daten heran und umgekehrt. Getrennte Datenbanken lassen sich außerdem einzeln sichern und wiederherstellen. EF lohnt sich im Worker nicht, weil er nur zwei Tabellen hat und per `COPY` schreibt.

## E-012 · Kein externer Erreichbarkeits-Check
**Entscheidung (F-007):** Nur Uptime Kuma, kein Drittanbieter.
**Bewusst in Kauf genommen:** Fällt der ganze VPS aus (Hardware, Netz, Kernel-Panik), kommt kein Alarm, weil Uptime Kuma selbst mit ausfällt. Das lässt sich später mit einem externen Check nachrüsten.

## E-013 · Erst alles lokal, dann der VPS
**Entscheidung (F-008, F-009):** Monitoring, Alarme und Backups werden zuerst lokal auf dem PC gebaut und getestet, **vor** dem Deploy auf den VPS. In der Roadmap sind die Phasen 7 und 8 deshalb gegenüber dem Auftrag getauscht. Lokal gehen Alarm-Mails an Mailpit und Backups in einen lokalen Ordner. Auf dem VPS sind die Monitoring-Oberflächen später nur per SSH-Tunnel erreichbar.
**Warum das funktioniert:** Genau dafür gibt es Docker. Dieselben Compose-Dateien laufen lokal und auf dem Server, nur die Umgebungsvariablen unterscheiden sich. Die CI auf GitHub braucht keinen VPS.
**Einschränkung lokal:** Unter Docker Desktop misst node-exporter die Linux-VM von Docker, nicht Windows selbst. Für die Festplatten-Warnung ist das zum Üben in Ordnung, echte Werte gibt es erst auf dem VPS.

## E-014 · Alarme und Benachrichtigungen nur per E-Mail
**Entscheidung (F-010):** Alarme und Hinweise auf neue Anmeldungen kommen per E-Mail. Den Discord-Webhook aus dem Auftrag („optional“) baue ich trotzdem ein. Er bleibt aber aus, bis eine Umgebungsvariable gesetzt ist.

## E-015 · Eigenes CSS statt Bootstrap, Schriften im Repo
**Entscheidung:** Kein CSS-Framework, sondern ein eigenes Stylesheet mit Design-Tokens (CSS-Variablen). Inter (Fließtext) und Space Grotesk (Überschriften) als variable WOFF2-Dateien unter `wwwroot/fonts`, Lizenz (SIL OFL) liegt daneben.
**Warum:** Die Webapp-Vorlage bringt Bootstrap und jQuery mit, zusammen gut 300 KB, von denen wir kaum etwas brauchen. Der Auftrag will wenig JavaScript und Lighthouse über 90. Selbst gehostete Schriften bedeuten: Der Browser fragt keinen fremden Server an, es wird keine IP-Adresse an Google übertragen, und es ist kein Cookie-Banner nötig.
**Hell/Dunkel:** Standard ist die Systemeinstellung (`prefers-color-scheme`). Der Schalter speichert die Wahl nur im Browser (`localStorage`). Ohne JavaScript bleibt er versteckt.

## E-016 · Statische Dateien über `MapStaticAssets`
**Entscheidung:** `app.MapStaticAssets()` statt `UseStaticFiles()`.
**Warum:** Ab .NET 9 werden CSS, JS und Schriften beim Build komprimiert (gzip/Brotli) und bekommen einen Fingerprint im Namen (`site.1mu24v7f3n.css`). Dadurch dürfen Browser sie dauerhaft cachen, und nach einer Änderung lädt trotzdem jeder die neue Version. Das zahlt direkt auf die Lighthouse-Performance ein.
**Stolperstein:** Die Fingerprint-URLs funktionieren nur im Modus `Development` oder in einem veröffentlichten Build (`dotnet publish`). Zum Starten deshalb `ASPNETCORE_ENVIRONMENT=Development` setzen. Achtung: `dotnet run --environment X` setzt in .NET 10 eine **Umgebungsvariable** mit dem Namen X, nicht die ASP.NET-Umgebung.

## E-017 · Sprachen über das URL-Präfix, Cookie nur nach Klick
**Entscheidung:**
- Jede Seite hat ein Sprachpräfix (`/de/...`, `/en/...`). Gesetzt wird es zentral über eine Razor-Pages-Konvention (`CultureRouteConvention`). Erlaubt sind nur `de` und `en` in Kleinbuchstaben.
- `/` leitet weiter: gewählte Sprache (Cookie) → Browsersprache (`Accept-Language`) → Deutsch.
- Der Umschalter (`/lang/{de|en}?returnPath=…`) setzt das Cookie `pubpot_lang` **nur beim Klick**.
- Sprachvorschlag: Ohne Cookie und bei abweichender Browsersprache erscheint eine Leiste in der vorgeschlagenen Sprache.
- Texte liegen in `Resources/SharedResource(.en).resx` (Layout) und `Resources/Pages/<Seite>(.en).resx`. Deutsch ist die neutrale Grundsprache.
- `hreflang` für de, en und `x-default` (= `/`), dazu `canonical`. Fehlerseiten bekommen `noindex`.

**Warum:** Eigene URLs pro Sprache sind für Suchmaschinen eindeutig, und Links lassen sich in einer festen Sprache teilen. Ein Cookie, das erst nach einem Klick gesetzt wird, dient einer ausdrücklich gewünschten Funktion. Ein Banner ist dafür nicht nötig (für die Datenschutzerklärung notiert, anwaltlich prüfen lassen).
**Sicherheit:** `returnPath` akzeptiert nur lokale Pfade mit Sprachpräfix, alles andere führt zur Startseite. Das verhindert Open Redirects wie `?returnPath=//evil.com`.
**Alternative:** Sprache nur per Cookie oder Header, ohne eigene URL. Das ist schlechter für SEO, und Links lassen sich nicht sprachgenau teilen.

## E-018 · Strikte CSP und Security-Header von Anfang an
**Entscheidung:** Eine Middleware (`Security/SecurityHeaders.cs`) setzt auf jede Antwort: CSP nur mit `'self'` (kein `unsafe-inline`, kein `unsafe-eval`, keine Inline-Styles), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, `Permissions-Policy` und `Cross-Origin-Opener/Resource-Policy`. Dazu kommt HSTS für ein Jahr inklusive Subdomains, aber ohne `preload`. Der `Server`-Header ist abgeschaltet. Die Cookie-Policy gilt für alle Cookies: HttpOnly, SameSite=Lax, Secure bei HTTPS.
**Warum jetzt:** Eine CSP nachträglich einzuführen ist mühsam, weil dann überall Inline-Skripte und -Styles auffallen. Von Anfang an strikt heißt: Jede Verletzung zeigt sich sofort im Browser.
**Folge für die Entwicklung:** Kein `style="…"` und kein `<script>…</script>` im Markup. Styles gehören nach `site.css`, Skripte als Datei nach `wwwroot/js`.
**Noch offen (Phase 5):** HSTS und `upgrade-insecure-requests` greifen nur, wenn die App HTTPS erkennt. Hinter Caddy kommt das über `X-Forwarded-Proto`, dafür braucht es die Forwarded-Headers-Middleware. → Erledigt in 5.1 (E-033).
**Warum kein `preload`:** Ein Eintrag in die HSTS-Preload-Liste der Browser lässt sich kaum rückgängig machen. Das entscheiden wir, wenn die Domain sicher feststeht.

## E-019 · Was die Tests absichern (und warum so)
**Entscheidung:** Integrationstests mit `WebApplicationFactory`: Die echte App läuft im Speicher, ohne Port und ohne Browser. Dazu kommen Tests, die Quelldateien direkt prüfen (`.resx`, `site.css`).
**Die Leitidee:** Was ich einmal von Hand geprüft habe (curl, Screenshots, Kontrast-Rechnung), wird ein Test. Dann prüft die CI es bei jedem Pull Request erneut.
**Gegenprobe:** Zwei Fehler absichtlich eingebaut (Kontrast zu niedrig, englischer Text fehlt). Beide Tests wurden rot.
**Lernpunkt:** Fehlt ein englischer Text, zeigt .NET still den deutschen an, ohne Fehler und ohne sichtbaren Schlüssel. Nur der Paritätstest (`ResourceTests`) findet das.
**Nicht automatisiert:** Lighthouse. Dafür braucht es Chrome in der CI, das kommt in Phase 6 als eigener, optionaler Schritt dazu.

## E-020 · Datenmodell und Ablauf der Anmeldung (Phase 3)
**Entscheidungen:**
1. **Eine Anmeldung pro E-Mail und Rolle.** Meldet sich dieselbe Adresse noch einmal als Entwickler an, wird der bestehende Eintrag aktualisiert und kein zweiter angelegt (Zusammenführen, wie im Auftrag). Meldet sich jemand als Entwickler **und** als Creator an, gibt es zwei Einträge, weil das zwei verschiedene Akquise-Vorgänge sind. Die E-Mail wird für den Vergleich normalisiert (Leerzeichen weg, Kleinschreibung).
2. **Änderungen an bestätigten Einträgen erst nach erneuter Bestätigung.** Sonst könnte jeder, der eine fremde E-Mail-Adresse kennt, deren Daten überschreiben. Die neuen Angaben warten deshalb, bis der Link in der neuen Mail angeklickt wurde.
3. **Bestätigungs-Token:** 32 Zufallsbytes im Link, in der Datenbank steht nur der SHA-256-Hash. Wer die Datenbank liest, kann damit keine Anmeldung bestätigen. Gültigkeit: 30 Tage, passend zur automatischen Löschung.
4. **Bestätigen per Button (POST), nicht schon beim Öffnen des Links (GET).** Viele Mail-Programme und Virenscanner öffnen Links in Mails automatisch vorab. Ein reiner GET würde Anmeldungen bestätigen, ohne dass ein Mensch geklickt hat.
5. **Mail-Ausgang (Outbox) in der Datenbank.** Das Formular speichert die Mail in einer Tabelle, ein Hintergrunddienst verschickt sie mit Wiederholungen. Ist der SMTP-Server kurz weg, geht die Anmeldung trotzdem nicht verloren, und das Formular wartet nicht auf den Mailserver.
6. **Keine IP-Adressen in der Datenbank.** Das Rate-Limiting arbeitet nur im Arbeitsspeicher (Datensparsamkeit).
7. **Spam wird still verworfen.** Füllt ein Bot den Honeypot aus oder ist er zu schnell, sieht er trotzdem die Danke-Seite. So lernt er nicht, was ihn verraten hat.

## E-021 · Mails: nur Text, Links nur aus `Site:BaseUrl`, Versand über Outbox
**Entscheidungen:**
- **Nur Text-Mails** (kein HTML). Weniger Angriffsfläche: Ein Name mit HTML darin kann nichts anrichten. Zustellbarkeit und Barrierefreiheit sind gut, und es gibt kein Template, das gepflegt werden muss.
- **Absolute Links nur aus `Site:BaseUrl`.** Außerhalb von Development bricht die App ohne diese Einstellung beim Start ab. Grund ist die sogenannte Host-Header-Injection: Wer das Formular mit `Host: boese.example` abschickt, würde sonst echte PubPot-Mails mit fremden Links an beliebige Adressen auslösen.
- **SMTP über MailKit 4.18** (die bewährte 4er-Reihe, 5.0 ist ganz neu). `Smtp:Security` steht standardmäßig auf `StartTls` und verlangt damit Verschlüsselung. `None` gibt es nur lokal für Mailpit.
- **Outbox mit Wiederholungen:** 1, 2, 4, 8 … Minuten, höchstens 1 Stunde, maximal 10 Versuche. Ins Log kommt nur die Mail-ID, nie der Empfänger.
- **Nur eine Instanz verschickt.** Liefen mehrere App-Instanzen gleichzeitig, könnte eine Mail doppelt rausgehen. Dafür bräuchte es dann `SELECT … FOR UPDATE SKIP LOCKED`. Für V1 mit einem Container reicht das so.

## E-022 · Spam-Schutz in Schichten, Data-Protection-Schlüssel in der Datenbank
**Entscheidungen:**
- **Honeypot** (Feld „Website“, außerhalb des sichtbaren Bereichs, `aria-hidden`, `tabindex=-1`) und **Mindestausfüllzeit** (3 s, Zeitstempel per Data Protection signiert und verschlüsselt). Ein Treffer wird **still verworfen**: Der Bot sieht die Danke-Seite, gespeichert wird nichts.
- **Fehlender oder ungültiger Zeitstempel** wird nicht still verworfen. Dann erscheint ein Hinweis „bitte erneut absenden“, und die Eingaben bleiben erhalten. Grund: Das kann auch einen Menschen treffen, etwa wenn das Formular sehr lange offen war.
- **Rate-Limiting** nur für POST: 5 pro IP und 10 Minuten. Bei Überschreitung kommt 429 mit eigener Seite. Die IP ist nur Schlüssel im Arbeitsspeicher, sie wird nie gespeichert oder geloggt.
- **Sperrfrist für Mails:** höchstens eine Bestätigungsmail pro Anmeldung alle 5 Minuten. Das schützt Dritte davor, mit Mails geflutet zu werden.
- **Data-Protection-Schlüssel in der Datenbank** (`data_protection_keys`, eigene Migration). Im Container würden sie bei jedem Neustart verloren gehen. Dann wären offene Formulare ungültig, und später würden alle Admins abgemeldet.
- **Statusseite `/status/{code}`** statt einer reinen 404-Seite: Sie zeigt passende Texte für 404, 429 und alles andere.

**Offen für Phase 5:** Hinter Caddy sieht die App ohne Forwarded Headers nur die IP von Caddy. Alle Besucher würden sich dann ein Rate-Limit teilen. → Erledigt in 5.1 (E-033).

## E-023 · Aufbewahrung bestätigter Anmeldungen (F-013)
**Entscheidung:**
1. Eine bestätigte Anmeldung gilt als **inaktiv**, wenn 12 Monate lang nichts passiert ist. Aktivität ist entweder ein Klick auf „Ich möchte dabei bleiben“ oder ein aktiver Admin-Status („kontaktiert“, „Gespräch“, „Pilot“) bzw. eine Änderung im Admin-Bereich.
2. Dann geht eine **Warnmail** raus: „Deine Daten werden in 30 Tagen gelöscht“, mit dem Link „Ich möchte dabei bleiben“. Den bestätigt man wie beim Double-Opt-in per Button, damit Mail-Scanner ihn nicht auslösen.
3. Ein Klick löst eine **Bestätigungsmail** aus („Löschung verhindert“), und die 12 Monate beginnen neu.
4. Ohne Klick wird die Anmeldung **30 Tage nach der Warnmail gelöscht**.
5. Ein erneut abgeschicktes Formular zählt **nicht** als Aktivität (Wunsch aus F-013).

**Umsetzung:** nach Phase 4 (eigener Schritt), mit einer neuen Spalte für die letzte Aktivität und dem Zeitpunkt der Warnmail. Die Datenschutzerklärung beschreibt die Frist erst, wenn sie gebaut ist. Anwaltlich prüfen lassen.

**Ergänzung (F-015):** Als Aktivität zählt nur eine **Änderung** im Admin-Bereich (Status, Notiz, Datum), nicht der Status an sich. Auch ein Eintrag, der 12 Monate unverändert auf „Pilot“ steht, bekommt also die Warnmail. **Warum:** Die Frist ist so eindeutig und hängt nicht davon ab, ob ein Status vergessen wurde. Eine kurze Notiz reicht, um die 12 Monate neu zu starten.

## E-024 · Admin-Bereich nur auf Deutsch (F-014)
**Entscheidung:** Der Admin-Bereich unter `/admin` ist nur auf Deutsch. Die Texte stehen direkt in den Razor-Seiten, nicht in `.resx`, und es gibt keine `/de`- oder `/en`-Route.
**Warum:** Ihn nutzt nur eine Person. Eine zweite Sprache würde die Arbeit in Phase 4 um etwa ein Drittel erhöhen, ohne dass jemand davon etwas hat. Der Paritätstest gilt weiter für alle öffentlichen Seiten.
**Alternative:** Wie die öffentlichen Seiten über `.resx` in DE/EN. Das lohnt sich erst, wenn weitere Admins dazukommen, die kein Deutsch sprechen.

## E-025 · Admin-Passwort als PBKDF2-Hash, Hilfsbefehl in der Web-App
**Entscheidungen:**
- **Nur der Hash steht in der Konfiguration** (`Admin__PasswordHash`), nie das Passwort. Wer die Umgebungsvariablen oder ein Backup der `.env` sieht, kennt das Passwort damit noch nicht.
- **Verfahren:** `PasswordHasher` aus ASP.NET Core Identity (PBKDF2 mit HMAC-SHA512, 100 000 Runden, zufälliges Salt). Er ist schon in ASP.NET Core enthalten, also kommt kein neues Paket dazu. Das Verfahren ist absichtlich langsam, damit ein gestohlener Hash nicht schnell durchprobiert werden kann. Das Salt sorgt dafür, dass dasselbe Passwort jedes Mal einen anderen Hash ergibt.
- **Der Hash ist Base64**, also ohne `$`. Andere Formate (bcrypt, Argon2) beginnen mit `$2b$…` bzw. `$argon2id$…`. Docker Compose würde das `$` in einer `.env` als Variable lesen und den Wert still verstümmeln.
- **Hilfsbefehl in der Web-App** (`dotnet run --project src/PubPot.Web -- hash-password`) statt eines eigenen Tools. So funktioniert er später auch im Container (`docker compose run --rm web hash-password`). Das Passwort wird verdeckt abgefragt und landet nicht in der Shell-Historie.
- **Kein Hash, kein Admin:** Fehlt `Admin__PasswordHash`, ist der Bereich aus (404). Ein **kaputter** Hash (etwa beim Kopieren abgeschnitten) verhindert dagegen den Start (fail fast), sonst würde jeder Login scheitern, ohne dass man den Grund sieht.
**Alternative:** ASP.NET Core Identity komplett mit Benutzertabelle. Das ist für ein einziges Konto zu groß: Es bräuchte Migrationen, Registrierung und Passwort-Reset, die wir nicht wollen.

## E-026 · Admin-Login: Cookie, Sperre und Sitzungen
**Entscheidungen:**
- **Cookie-Login** mit dem eingebauten Cookie-Schema von ASP.NET Core. Das Cookie ist mit Data Protection verschlüsselt und signiert. Weil die Schlüssel in der Datenbank liegen, überlebt ein Login einen Neustart (E-022).
- **Cookie `__Host-pubpot-admin`** mit `Secure`, `HttpOnly` und `SameSite=Strict`. Es ist ein Sitzungscookie und endet also, wenn der Browser geschlossen wird. Das Präfix `__Host-` zwingt den Browser, das Cookie nur mit Secure, ohne Domain und mit `Path=/` anzunehmen, sodass keine Subdomain es setzen kann. `Strict` heißt: Bei Anfragen von fremden Seiten fehlt das Cookie, auch beim Klick auf einen Link in einer Mail. Dann kommt zuerst der Login und danach direkt die gewünschte Seite.
- **Sitzungsdauer:** 8 Stunden ohne Aktivität (jede Anfrage verlängert), höchstens 24 Stunden insgesamt.
- **Sperre für das Konto:** Nach 5 Fehlversuchen in Folge ist der Login für 15 Minuten gesperrt, auch für das richtige Passwort. Dazu kommt ein **Rate-Limit pro IP** (10 Login-Versuche in 10 Minuten). Die Kontosperre hilft auch gegen Angreifer mit vielen IP-Adressen. Der Preis: Ein Angreifer kann den Login zeitweise blockieren. Für ein einziges Admin-Konto ist das vertretbar. Die IP-Erkennung hinter Caddy kommt in Phase 5.
- **Gleich lange Prüfung:** Benutzername und Passwort werden immer beide geprüft, und der Name wird in fester Zeit verglichen. So verrät die Antwortzeit nicht, welcher Teil falsch war.
- **Abmelden sperrt die Sitzung:** Ein Cookie trägt alles selbst und wäre sonst nach dem Abmelden weiter gültig, falls jemand es kopiert hat. Die abgemeldeten Sitzungen stehen in einer Liste im Arbeitsspeicher. **Grenze:** Nach einem Neustart ist die Liste leer, und ein vorher kopiertes Cookie gilt wieder bis zu seinem Ablauf (höchstens 24 Stunden). Für V1 reicht das. Sonst bräuchte es eine Tabelle.
- **Passwortwechsel beendet alle Sitzungen:** Das Cookie enthält einen Fingerabdruck von Benutzername und Passwort-Hash. Passt er nicht mehr, wird man abgemeldet.
- **Schutz aller Seiten über eine Konvention** (`AuthorizeFolder("/Admin")`) statt einzeln per Attribut. Ein Test findet alle Admin-Seiten selbst und prüft, dass jede außer dem Login geschützt ist.
- **Nichts zwischenspeichern:** Admin-Seiten senden `Cache-Control: no-store` und `X-Robots-Tag: noindex`. Der Pfad `/admin` steht bewusst nicht in `robots.txt`, weil die Datei öffentlich ist und den Pfad nur bekannter machen würde.
- **Ins Log** kommen Fehlversuche und Sperren, aber nie der eingegebene Benutzername. Dort steht oft versehentlich das Passwort.
**Alternative:** ASP.NET Core Identity mit Benutzertabelle, Lockout und Sicherheitsstempel in der Datenbank. Das ist für ein Konto zu groß (siehe E-025). Für mehrere Admins wäre es der richtige Weg.

## E-027 · Admin-Liste: Filter in der URL, Suchtext nur per POST
**Entscheidungen:**
- **Filter ohne Personenbezug** (Art, bestätigt, Status, Sprache, Seite) stehen in der URL, z. B. `/admin?kind=Developer&status=Contacted`. So funktionieren Neu laden, Zurück und Lesezeichen.
- **Der Suchtext geht nur per POST.** Er enthält oft eine E-Mail-Adresse oder einen Namen. In einer URL würde er in Server-Logs (später Caddy), im Browserverlauf und im Referrer landen. Der Auftrag sagt: „keine personenbezogenen Daten in URLs“. Ein Suchtext in der URL wird ignoriert.
- **Post/Redirect/Get:** Wird das Formular ohne Suchtext abgeschickt, leitet die Seite auf die passende GET-Adresse weiter. Mit Suchtext wird das Ergebnis direkt angezeigt. Neu laden fragt dann „Formular erneut senden?“, das ist der Preis.
- **Seitenwechsel über Buttons im selben Formular** (`name="p"`), damit Filter und Suchtext erhalten bleiben. `page` geht als Name nicht, weil Razor Pages diesen Routenwert selbst belegt (Seitenpfad).
- **Suche mit `ILIKE`** (Groß-/Kleinschreibung egal) in Name, E-Mail, Studio, Spiel und Kanal. `%` und `_` aus der Eingabe werden maskiert und gelten als normale Zeichen. Ohne Volltextindex: Bei ein paar hundert Zeilen reicht ein Durchsuchen der Tabelle.
- **50 pro Seite**, neueste zuerst. Eine zu große Seitenzahl springt auf die letzte Seite.
**Alternative:** Alles per GET (einfacher, aber E-Mail-Adressen in Logs) oder alles per POST (keine Lesezeichen, kein Zurück).

## E-028 · Löschen für DSGVO-Löschanfragen
**Entscheidungen:**
- **Endgültig löschen statt nur verstecken.** Wer eine Löschung verlangt, hat Anspruch darauf, dass die Daten wirklich weg sind. Ein „gelöscht“-Kennzeichen würde die Daten weiter speichern.
- **Was gelöscht wird:** die Anmeldung, ihre Bestätigungslinks (per `ON DELETE CASCADE`) und **alle** Mails an diese Adresse im Mail-Ausgang, auch schon versendete. Die enthalten die Adresse und würden sonst noch bis zu 7 Tage gespeichert. Die Adresse wird ohne Rücksicht auf Groß-/Kleinschreibung verglichen. Alles läuft in einer Transaktion.
- **Ausnahme:** Gibt es unter derselben Adresse noch eine Anmeldung in der anderen Rolle, bleiben die Mails erhalten, weil sie zu dieser gehören könnten. Die Rückfrage-Seite weist darauf hin und verlinkt die andere Anmeldung. Bei einer Löschanfrage löscht man dann beide.
- **Zwei Schritte:** Erst eine Seite „Wirklich löschen?“ (GET ändert nichts), dann der Button (POST mit Antiforgery-Token). Ein versehentlicher Klick oder ein vorab geladener Link löscht so nichts.
- **Ins Log kommt nur die ID** und die Zahl der entfernten Mails, nie Name oder Adresse. Sonst stünde die gelöschte Person weiter im Log.
- **Grenze:** In Backups (Phase 7) bleiben die Daten bis zur Rotation erhalten. Das muss in der Datenschutzerklärung stehen (anwaltlich prüfen lassen).
**Alternative:** Anonymisieren (Name und Adresse überschreiben, Rest behalten), z. B. für Statistiken. Für V1 gibt es keine Statistik, die das bräuchte.

## E-029 · CSV-Export für Excel, mit Schutz vor CSV-Injection
**Entscheidungen:**
- **Export der gefilterten Liste:** Der Button „CSV exportieren“ sitzt im Filterformular der Liste und schickt es per `formaction` an `/admin/export`. Filter und Suchtext kommen so mit, ohne dass der Suchtext in einer URL steht (E-027).
- **Format für deutsches Excel:** UTF-8 **mit BOM** (ohne BOM zeigt Excel Umlaute falsch an), **Semikolon** als Trenner (Excel mit deutscher Einstellung erwartet es so), Zeilenende CRLF, Felder mit `;`, `"` oder Zeilenumbruch in Anführungszeichen (RFC 4180). Datumsangaben deutsch und in der Zeitzone Europe/Berlin.
- **CSV-Injection:** Beginnt eine Zelle mit `=`, `+`, `-`, `@`, Tab oder CR, kommt ein `'` davor. Sonst würde Excel einen Namen wie `=HYPERLINK("https://boese.example")` als Formel ausführen. Die Daten stammen aus einem öffentlichen Formular, also von Fremden. Der Preis: Eine Telefonnummer wie `+49…` erscheint als `'+49…`.
- **Alle Spalten** inklusive Notiz und Einwilligung (Version und Zeitpunkt). Damit lässt sich auch eine DSGVO-Auskunft beantworten.
- **Dateiname ohne persönliche Daten:** `pubpot-anmeldungen-JJJJ-MM-TT.csv`. Ins Log kommt nur die Anzahl der Zeilen.
- **Ohne Bibliothek:** Ein Paket wie CsvHelper wäre für 27 Spalten zu viel. Das Maskieren ist in einer kleinen, getesteten Methode (`SignupCsv.Cell`).
**Alternative:** Excel-Datei (`.xlsx`). Das bräuchte eine Bibliothek, ist schwerer zu prüfen, und CSV öffnet jedes Programm.

## E-030 · Hinweise auf neue Anmeldungen: Mail über die Outbox, Discord optional
**Entscheidungen:**
- **Auslöser** ist nur die **erste** Bestätigung einer Anmeldung, nicht die Bestätigung einer Änderung.
- **Mail über die Outbox** an `Notifications__Email`, gespeichert im **selben** `SaveChanges` wie die Bestätigung. Entweder klappt beides oder nichts, und eine Bestätigung ohne Hinweis gibt es nicht. Der Versand hat dieselben Wiederholungen wie jede andere Mail (E-021).
- **Keine persönlichen Daten in den Hinweisen:** nur Art (Entwickler/Creator), Sprache und der Link zur Detailseite. Name und Adresse sieht man nach dem Login. So liegen sie nicht zusätzlich in einem Postfach oder bei Discord (US-Anbieter).
- **Discord nur mit `Notifications__DiscordWebhookUrl`.** Die URL ist ein Geheimnis, denn wer sie kennt, kann in den Kanal schreiben. Beim Start wird geprüft, dass es eine `https://discord.com/api/webhooks/…`-Adresse ist. `allowed_mentions` ist leer, damit nie jemand angepingt wird.
- **Die Webhook-URL steht nie im Log:** `IHttpClientFactory` loggt standardmäßig jede aufgerufene URL. Für den Discord-Client ist das mit `RemoveAllLoggers()` abgeschaltet. Ein Test prüft das, und ohne die Zeile stünde die URL viermal im Log.
- **Discord-Warteschlange im Arbeitsspeicher** (höchstens 100, 3 Versuche nach 2 und 4 Sekunden). Nach einem Neustart sind offene Discord-Hinweise verloren. Das ist vertretbar, weil die Mail der verlässliche Weg ist.
- **Ohne `Notifications__Email`** gibt es keine Hinweis-Mail, dafür eine Warnung beim Start. Die App startet trotzdem: Lokal braucht man keine Hinweise.
**Alternative:** Discord ebenfalls über eine Tabelle in der Datenbank (verlässlich auch über Neustarts). Das wäre eine weitere Migration für einen optionalen Kanal, der laut E-014 standardmäßig aus ist.

## E-031 · Aufbewahrungsfrist umgesetzt: Spalten, Job, Link „Ich möchte dabei bleiben“
Umsetzung von E-023 (F-013, F-015).
**Entscheidungen:**
- **Drei neue Spalten** (Migration `Retention`): `last_activity_at`, `retention_warning_sent_at` und `retention_token_hash`. Bestehende bestätigte Anmeldungen bekommen als letzte Aktivität den späteren Zeitpunkt von Bestätigung und Admin-Änderung (`GREATEST`, in der Migration von Hand ergänzt und getestet).
- **Was als Aktivität zählt** (`Signup.RecordActivity`): die **erste** Bestätigung, ein Klick auf „Ich möchte dabei bleiben“ und jede Änderung im Admin-Bereich (F-015). **Nicht** dazu gehören ein erneut abgeschicktes Formular und auch dessen bestätigte Änderung (F-013). Eine Aktivität setzt eine laufende Warnung zurück, dann wird auch nicht gelöscht.
- **Job im vorhandenen Aufräum-Dienst** (stündlich): zuerst löschen (Warnmail mindestens 30 Tage alt), dann warnen (12 Monate ohne Aktivität, die ältesten zuerst, höchstens 200 pro Durchlauf). Gelöscht wird über denselben Baustein wie im Admin-Bereich (`SignupDeletion`), also inklusive der Mails an die Adresse.
- **Link wie beim Double-Opt-in:** zufälliges Token, in der Datenbank nur der SHA-256-Hash, und die Seite `/{sprache}/signup/keep` bestätigt erst per Button (POST). Mail-Scanner öffnen Links vorab und würden sonst die Löschung verhindern, ohne dass die Person etwas getan hat. Der Link gilt nur einmal.
- **Mails in der Sprache der Anmeldung**, als Text wie alle Mails (E-021), mit dem Löschdatum in deutscher Zeit. Die Texte werden erst innerhalb von `InCulture` gelesen, sonst kämen sie im Hintergrund-Job in der Sprache des Servers.
- **`Site:BaseUrl` auch lokal gesetzt** (`appsettings.Development.json`, `http://localhost:5080`): Der Job läuft ohne Anfrage im Hintergrund und kann den Link sonst nicht bauen. In Tests steht `http://localhost`.
- **Datenschutzerklärung** beschreibt die Frist jetzt (DE/EN). Die Aufbewahrung in Backups ergänze ich in Phase 7 (TODO, anwaltlich prüfen lassen).
**Alternative:** Ein eigener Hintergrunddienst nur für die Frist. Der vorhandene Aufräum-Dienst läuft ohnehin stündlich, ein zweiter Timer bringt nichts.

## E-032 · Rahmen für Phase 5: Worker per Profil, lokal HTTPS mit Caddys eigenem Zertifikat
**Entscheidungen (F-016, F-017):**
- **Worker nur mit Compose-Profil `worker`.** `docker compose up` startet ihn nicht, `docker compose --profile worker up` schon. Auf dem VPS schaltet `COMPOSE_PROFILES=worker` in der `.env` ihn dauerhaft ein.
- **Lokal HTTPS unter `https://localhost`.** Caddy stellt das Zertifikat mit seiner eigenen Zertifizierungsstelle aus. Die Warnung im Browser wird vorerst weggeklickt, die Anleitung zum Vertrauen steht trotzdem in der README.
**Warum:** Die Studie (F-001) läuft noch mit denselben Twitch-Zugangsdaten. Zwei Worker teilen sich das Limit pro Client-ID und könnten die Studie stören. Mit HTTPS verhalten sich Secure-Cookies, das `__Host-`-Admin-Cookie und HSTS lokal genauso wie auf dem Server. Mit reinem HTTP ließe sich genau das nicht testen.
**Folge:** Die Abnahme in 5.8 prüft beim Worker Image, Schema-Skript und Healthcheck, aber keine echte Runde gegen Twitch.

## E-033 · Forwarded Headers nur vom vertrauten Proxy-Netz
**Entscheidung:** Die App übernimmt `X-Forwarded-For` und `X-Forwarded-Proto` nur von Absendern aus `Proxy__KnownNetworks` (CIDR, kommagetrennt). In Compose ist das das feste Netz von Caddy (Schritt 5.6). `X-Forwarded-Host` wird nicht übernommen. Es zählt nur ein Proxy (`ForwardLimit = 1`). Die Middleware läuft als Erstes, damit HSTS, Cookies und Rate-Limiting schon die echten Werte sehen.
**Warum:** Ohne diese Header sieht die App hinter Caddy nur Caddys IP, und alle Besucher teilen sich ein Rate-Limit. Außerdem hält sie jede Verbindung für HTTP, dann gibt es weder HSTS noch Secure-Cookies (E-018, E-022). Wer die Header aber von jedem annimmt, lässt Angreifer ihre IP frei wählen und das Rate-Limiting umgehen.
**Falle:** Sind in ASP.NET **keine** vertrauten Proxys eingetragen, übernimmt die Middleware die Header von **jedem** Absender. Ohne `Proxy__KnownNetworks` (etwa bei `dotnet run`) ist die Middleware deshalb gar nicht eingeschaltet. Ein Test prüft das. Ein ungültiger Eintrag bricht den Start ab.
**Docker-Detail:** Bei Dual-Stack meldet Kestrel IPv4-Absender oft als `::ffff:172.30.0.2`. Die Middleware erkennt solche Adressen, ein Test deckt es ab.
**Host-Header:** Links in Mails kommen aus `Site:BaseUrl` (E-021). Deshalb braucht die App den ursprünglichen Host nicht, und ein gefälschter Host kann nichts anrichten.

## E-034 · Web-Image „chiseled“ mit eingebautem Healthcheck
**Entscheidungen:**
- **Zwei Stufen:** Gebaut wird mit `sdk:11.0` (E-007), ins fertige Image kommt nur das Ergebnis von `dotnet publish`. Das SDK, der Quellcode und die NuGet-Pakete bleiben in der Build-Stufe.
- **Laufzeit `aspnet:10.0-noble-chiseled-extra`:** Ubuntu ohne Shell, ohne Paketmanager und ohne root-Benutzer (läuft als `app`, UID 1654). „extra“ enthält ICU und Zeitzonen-Daten, die wir für Deutsch/Englisch und `Europe/Berlin` brauchen. Ohne „extra“ liefe .NET im Invariant-Modus, dann wären Datumsformate und Sortierung falsch.
- **Healthcheck als eigener Befehl** `dotnet PubPot.Web.dll healthcheck`: fragt `/health` im Container ab, Exit-Code 0 oder 1. Im Image gibt es kein `curl`, und eins nachzuinstallieren hieße, die Schlankheit wieder aufzugeben.
- **Restore als eigene Schicht:** Erst die `.csproj` und die zentralen Props kopieren und `restore` ausführen, dann den Code. Ändert sich nur Code, kommt das Restore aus dem Docker-Cache.
- **`GssEncryptionMode=Disable`** im Connection-String (wie beim Worker). Sonst sucht Npgsql `libgssapi_krb5` und schreibt eine verwirrende Fehlermeldung ins Log.
**Warum chiseled:** Was nicht im Image ist, kann ein Angreifer nicht benutzen. Ohne Shell kann eine Lücke in der App nicht einfach `sh` starten. Außerdem meldet ein Image-Scanner weniger Pakete mit bekannten Lücken. Der Preis: `docker exec … sh` geht nicht. Zum Nachsehen nimmt man `docker logs` oder startet einen Debug-Container daneben.
**Größe:** 261 MB entpackt.

## E-035 · Migrations-Bundle als eigenes Image, nur mit Connection-String
**Entscheidungen:**
- **Stufe `migrate` im selben Dockerfile** wie die Web-App. Das Bundle (`efbundle`) ist eine einzelne Datei mit allen Migrationen. Das Image `pubpot-migrate` läuft als einmaliger Container vor der App und endet mit Exit-Code 0 oder 1. Die App migriert nie selbst.
- **`AppDbContextFactory`** (`IDesignTimeDbContextFactory`): `dotnet ef` und das Bundle erzeugen den DbContext darüber und brauchen nur `ConnectionStrings__Default` (lokal aus den User Secrets). Die Einstellungen (snake_case, ohne GSSAPI) stehen in `AppDbContext.Configure` und gelten für App und Werkzeuge gleich.
- **Connection-String als Umgebungsvariable**, nicht als `--connection`-Argument. Argumente stehen in `docker inspect` und in der Prozessliste.
- **.NET-10-Laufzeit in der Build-Stufe:** `dotnet ef` lädt beim Erzeugen die App, und die läuft auf .NET 10. Das SDK-11-Image bringt nur die Laufzeit 11 mit. Die 10er kommt per `COPY --from` aus dem offiziellen Laufzeit-Image, und zwar nur in die Stufe, die das Bundle baut.
**Warum nicht beim Start der App migrieren:** Bei mehreren Instanzen würden alle gleichzeitig migrieren, und eine fehlgeschlagene Migration ließe eine halb gestartete App zurück. Als eigener Schritt ist die Reihenfolge klar: Migration erfolgreich → App startet. Beim Deploy (Phase 8) kann das Skript außerdem abbrechen, bevor die alte Version gestoppt wird.
**Warum die Factory:** Ohne sie startet EF die ganze App bis kurz vor den Webserver. Dann bräuchte schon die Migration SMTP, `Site:BaseUrl` und den Admin-Hash.
**Getestet:** Gegen eine leere Datenbank (5 Migrationen, Exit 0), noch einmal (nichts zu tun, Exit 0), mit falschem Passwort (Exit 1, ohne das Passwort im Log), gegen die Entwicklungsdatenbank (nichts zu tun). Es läuft auch mit schreibgeschütztem Dateisystem.

## E-036 · Worker: Schema als SQL-Skripte, Image chiseled, eigene Tests
Umsetzung von E-011 für den Worker.
**Entscheidungen:**
- **Versionierte SQL-Skripte** in `deploy/db/twitch/` (`001_schema.sql`, später `002_…`). Der Runner `migrate.sh` läuft als einmaliger Container im Postgres-Image, das `psql` schon mitbringt. Jedes Skript läuft genau einmal und **zusammen mit seinem Eintrag in `schema_migrations` in einer Transaktion**. Ein Fehler rollt alles zurück und beendet den Runner mit Fehlercode, dann startet der Worker nicht.
- **`001_schema.sql` mit `IF NOT EXISTS`:** Datenbanken, in denen der alte Worker die Tabellen schon selbst angelegt hat (etwa die der Studie, falls wir sie später übernehmen), laufen ohne Fehler durch, die Daten bleiben.
- **Der Worker legt nichts mehr an.** Beim Start prüft er nur, ob `snapshots` und `stream_samples` da sind. Fehlen sie, versucht er es 30-mal im Abstand von 2 Sekunden und bricht dann mit klarer Meldung ab.
- **Image `runtime:10.0-noble-chiseled`** ohne „extra“: Der Worker braucht weder Übersetzungen noch Zeitzonen (alles UTC). 146 MB statt 261 MB.
- **Healthcheck als Befehl** `dotnet TwitchWorker.dll healthcheck`: gesund, solange die Heartbeat-Datei höchstens 15 Minuten alt ist. Vorher hat das `find` in der Shell geprüft, im neuen Image gibt es keine Shell.
- **Eigenes Testprojekt** `tests/TwitchWorker.Tests`: Healthcheck, und der Runner läuft in Testcontainers im echten Postgres-Image (zweimal hintereinander, auf alten Tabellen, mit kaputtem Skript).
**Warum kein Werkzeug wie Flyway oder DbUp:** Der Worker hat zwei Tabellen. Ein eigenes Werkzeug wäre ein weiteres Image oder Paket, das gepflegt werden muss. 30 Zeilen Shell mit Tests reichen hier.
**Vorgemerkt für Phase 7:** Auch eine unvollständige Runde (Twitch nicht erreichbar) schreibt den Heartbeat. Der Healthcheck meldet dann „gesund“, obwohl keine Daten kommen. Für die Alarme sollte zusätzlich zählen, wie viele Runden vollständig waren.

## E-037 · Compose für die Anwendung: Reihenfolge, wenige Variablen pro Container, Härtung
**Entscheidungen:**
- **Startreihenfolge über `depends_on` mit Bedingungen:** `db` gesund → `migrate` mit Exit-Code 0 beendet → `web`. Genauso `db` → `migrate-twitch` → `worker`. Schlägt eine Migration fehl, startet die Anwendung gar nicht erst.
- **Jeder Container bekommt nur seine Variablen** (`environment:` mit `${…}`, kein `env_file:`). Die `.env` dient Compose nur zum Einsetzen. Die Web-App sieht also nie das Twitch-Secret, der Worker nie das SMTP-Passwort.
- **Variablennamen wie in der App** (`Smtp__Host`, `Admin__PasswordHash`), nur Infrastruktur in Großbuchstaben (`POSTGRES_PASSWORD`, `IMAGE_TAG`). So gibt es für jede Einstellung genau einen Namen.
- **Pflicht nur für die DB-Passwörter** (`:?` in Compose). Für alles andere setzt Compose leere oder sinnvolle Standardwerte, und die App prüft beim Start selbst (fail fast, mit klarer Meldung). Sonst würde Compose schon `docker compose up -d db` verweigern, solange ein App-Wert fehlt.
- **Lokale Werte nur in `compose.override.yaml`:** Mailpit als SMTP, `team@localhost.test` für die Hinweise, `build:` statt Image aus der Registry. Auf dem Server gibt es diese Datei nicht.
- **Härtung** für `web`, `worker` und die Migrationen: `read_only`, `/tmp` als tmpfs, `no-new-privileges`, `cap_drop: ALL`. Die Datenbank bleibt ohne, weil das Postgres-Image beim Start Rechte wechselt und in sein Volume schreibt.
- **Images** heißen schon wie in der Registry (`ghcr.io/tidragounity/pubpot-web` usw.) mit `IMAGE_TAG` (lokal `local`).
**Grenze: eine Web-Instanz pro Datenbank.** Der Mail-Ausgang sperrt keine Zeilen, und die Login-Sperre zählt im Arbeitsspeicher (E-026). Zwei Instanzen würden Mails doppelt verschicken. Für diese Seite reicht eine Instanz. Lokal heißt das: entweder `dotnet run` oder der Web-Container, nicht beides. Für mehrere Instanzen bräuchte der Ausgang `FOR UPDATE SKIP LOCKED`.

## E-038 · Caddy als einziger Eingang, getrennte Netze
**Entscheidungen:**
- **Caddy 2.11.9** vor der App. Die Domain kommt aus `SITE_DOMAIN`. Lokal (`localhost`) stellt Caddy das Zertifikat mit seiner eigenen Zertifizierungsstelle aus (F-017), auf dem Server holt er es automatisch bei Let's Encrypt. HTTP leitet er mit 308 auf HTTPS um, HTTP/3 läuft über 443/udp.
- **Zwei Netze:** `edge` (Caddy und web, festes Subnetz `172.30.0.0/24`) und `default` (alle anderen). Caddy kommt nicht an die Datenbank. Nur Caddy hat Ports nach außen, die App selbst nicht mehr.
- **Vertrauen nur für `edge`:** `Proxy__KnownNetworks=172.30.0.0/24` bei web (E-033). Caddy überschreibt mitgeschickte `X-Forwarded-*`-Header, und die App nimmt nur den letzten Eintrag. Ein gefälschter Header kommt also nicht durch.
- **Ports über `BIND_ADDRESS`**, Standard `127.0.0.1`. Lokal ist die Seite nur auf deinem Rechner erreichbar, auf dem Server kommt `0.0.0.0` in die `.env`. Eine Variable reicht, weil Compose `ports:` aus mehreren Dateien zusammenfügt statt ersetzt.
- **Kein Access-Log** (IP-Adressen sind personenbezogen), **`Server` und `Via` entfernt**, **`admin off`** (keine Verwaltungs-API im Container).
- **Kein `encode`:** Die statischen Dateien sind schon komprimiert (`MapStaticAssets`). HTML mit Antiforgery-Token komprimieren wir über HTTPS bewusst nicht (BREACH-Angriff). Lighthouse hatte auch ohne Kompression 100.
- **Gehärtet wie die eigenen Container:** schreibgeschützt, `cap_drop: ALL`, `no-new-privileges`. Caddy läuft im offiziellen Image zwar als root, darf aber ohne Sonderrechte nichts außer seinen eigenen Dateien schreiben. Die Ports 80 und 443 gehen trotzdem, weil Docker in Containern das Binden niedriger Ports erlaubt.
- **Healthcheck** über einen internen Port 2020 (`/health`), nicht veröffentlicht.
**Gut zu wissen:** Für `localhost` setzt ASP.NET absichtlich **kein HSTS**. Sonst würde der Browser `localhost` ein Jahr lang nur noch per HTTPS aufrufen, und `dotnet run` unter `http://localhost:5080` ginge nicht mehr. Dass HSTS hinter Caddy mit echter Domain kommt, prüfen die Tests aus 5.1.

## E-039 · Statusseiten ohne Antiforgery-Prüfung, Subnetz von „edge“ einstellbar
Zwei Funde aus der Abnahme 5.8.
- **Statusseiten (`/status/{code}`, `/error`) ignorieren das Antiforgery-Token.** Bei einem POST ruft ASP.NET die Statusseite ebenfalls per POST auf (Re-Execute). Ohne gültiges Token wurde daraus ein leeres 400: Aus „429 Bitte warte einen Moment“ oder „500“ wurde für Bots ohne Token und für Menschen mit abgelaufenem Formular eine leere Seite mit falschem Code. Die Statusseiten ändern nichts, also braucht es dort keine Prüfung. Ein Test deckt es ab.
- **`EDGE_SUBNET`** (Standard `172.30.0.0/24`) legt das Subnetz von „edge“ fest, und `Proxy__KnownNetworks` folgt automatisch. Docker vergibt jedes Subnetz pro Rechner nur einmal. Ein zweites Compose-Projekt (die Abnahme, später Staging neben Production) bräuchte sonst ein anderes.

## E-040 · Rahmen für Phase 6: öffentliche Images, ein PR pro Schritt
**Entscheidungen (F-018, F-019):**
- **Images in GHCR öffentlich.** Der Server zieht sie ohne Token. In den Images steckt nur, was ohnehin im öffentlichen Repo liegt. Angelegt von der CI des öffentlichen Repos, sind sie automatisch öffentlich (siehe E-043).
- **Ein Branch und ein Pull Request pro Roadmap-Schritt**, ab 6.1. Branch-Name `x.y-kurzname`, z. B. `6.1-ci`. Ist die CI grün, merge ich per „Squash and merge“ mit dem Titel `x.y: …`. Auf `main` bleibt so ein Commit pro Schritt wie bisher. ROADMAP, JOURNAL und START-SESSION ändern sich im selben PR.
- **6.1 gleich als PR:** Die neue CI prüft damit schon ihren eigenen Pull Request. Ab 6.7 erzwingen Regeln diesen Ablauf.
**Warum:** Erst durch Pull Requests hat die CI das letzte Wort. Ungeprüfter Code kommt nicht auf `main`, und jeder Schritt lässt sich auf GitHub mit Diff und CI-Ergebnis nachvollziehen.

## E-041 · CI-Workflow: dieselben Prüfungen wie lokal, festgenagelte Actions
**Entscheidungen:**
- **Die CI ruft `./scripts/check.sh` auf**, statt die Schritte im Workflow zu wiederholen. Damit gibt es nur eine Stelle, an der steht, was „grün“ heißt. In GitHub Actions gliedert das Skript seine Abschnitte als einklappbare Gruppen (`::group::`).
- **Fremde Actions auf Commit-SHA** (`actions/checkout@3d3c42e…  # v7.0.1`). Ein Tag wie `v7` kann der Autor oder ein Angreifer mit Zugriff jederzeit auf anderen Code umbiegen, ein SHA nicht. Der Kommentar zeigt die Version, und Dependabot (6.5) aktualisiert beides.
- **Minimale Rechte:** `permissions: contents: read` für den ganzen Workflow. `persist-credentials: false` beim Checkout, damit das Token nicht in `.git/config` liegen bleibt.
- **.NET:** SDK 11 aus `global.json` und zusätzlich `10.0.x`, weil die Tests auf der .NET-10-Laufzeit laufen (E-007).
- **NuGet-Cache** über `actions/cache` mit einem Schlüssel aus `Directory.Packages.props` und den `.csproj`-Dateien. Den eingebauten Cache von `setup-dotnet` nutzen wir nicht, weil er Lock-Dateien verlangt.
- **Abbruch alter Läufe** nur bei Pull Requests. Auf `main` läuft jeder Lauf zu Ende, weil er später Images baut (6.3).
- **Runner `ubuntu-24.04`** statt `ubuntu-latest`: Ein Wechsel der Ubuntu-Version kommt dann bewusst per Änderung, nicht unbemerkt.
**Abweichung vom Plan:** Keine Testergebnisse als Artefakt. Der Test-Runner (Microsoft Testing Platform mit xUnit v3) schreibt TRX nur mit einem Zusatzpaket, und das Log des Laufs zeigt fehlgeschlagene Tests mit Meldung und Stacktrace ohnehin.

## E-042 · Smoke-Test des Docker-Stacks als Skript, in der CI und lokal
**Entscheidungen:**
- **`scripts/smoke-test.sh`** startet den Stack nur aus `deploy/compose.yaml`, also ohne `compose.override.yaml`, ohne Build und mit fertigen Images (`IMAGE_TAG`), so wie auf dem Server. Dann prüft er: migrate endet mit 0, `migrate-twitch` läuft durch, `/health/ready` und `/de` über Caddy, HTTP → 308, kein `Server`-Header. Bei einem Fehler gibt er die Logs aller Container aus. Am Ende räumt er immer auf (`down -v`).
- **Eigenes Compose-Projekt** (`pubpot-smoke`), Wegwerf-Passwörter aus `/dev/urandom`, eigenes Subnetz (`EDGE_SUBNET=172.31.250.0/24`). Lokal stört er deine Daten nicht. Er braucht nur kurz die Ports 80 und 443.
- **In der CI:** Job „Images und Smoke-Test“ parallel zu „Build und Tests“. Buildx baut die drei Images mit dem Commit-SHA als Tag und lädt sie in das Docker des Runners. Der Build-Cache liegt im Cache von GitHub Actions (`type=gha`, je Image ein eigener Bereich). Die Image-Größen stehen in der Zusammenfassung des Laufs.
- **SMTP im Smoke-Test** zeigt auf `smtp.invalid`. Die App braucht einen Wert, um zu starten, verschickt im Test aber keine Mail.
**Warum:** Die Tests aus 6.1 prüfen den Code, aber nicht die Dockerfiles, die Compose-Datei, das Caddyfile und das Zusammenspiel. Genau dort sind in Phase 5 die meisten Fehler aufgetreten (Laufzeit im SDK-Image, `Via`-Header, Subnetz).

## E-043 · Images nach GHCR: nur von `main`, nur nach grüner CI, mit SHA-Tag
**Entscheidungen:**
- **Job `publish`** läuft nur bei einem Push auf `main` (also nach einem Merge) und nur, wenn „Build und Tests“ und „Images und Smoke-Test“ grün sind (`needs`). In Pull Requests wird er übersprungen. Ein PR kann also nie ein Image veröffentlichen.
- **Tags:** der volle Commit-SHA (unveränderlich, damit deployt Phase 8 genau den geprüften Stand) und `main` (zeigt immer auf den neuesten Stand, praktisch zum Ausprobieren, nie für den Deploy).
- **Anmeldung mit dem automatischen `GITHUB_TOKEN`**, kein eigenes Secret. `packages: write` gibt es nur in diesem einen Job.
- **Gleicher Build wie im Smoke-Test:** Der Job baut aus demselben Cache. Alle Schichten sind dort schon vorhanden, es entsteht also derselbe Inhalt, ohne die Images zwischen Jobs als Datei herumzureichen.
- **SBOM und Provenance** hängen als Attestierungen am Image. Die SBOM listet alle enthaltenen Pakete. Die Provenance sagt, aus welchem Commit und mit welchem Workflow das Image gebaut wurde. Beides wertet später auch Trivy aus.
- **OCI-Labels** über `docker/metadata-action`. `org.opencontainers.image.source` verknüpft das Package mit dem Repo.
- **Werte aus `${{ }}` nur über `env:` in Skripte** (Schutz vor Script Injection), auch wenn sie hier harmlos sind. So bleibt es eine feste Regel.
**Nach dem ersten Push:** Entgegen der Erwartung waren die Packages sofort öffentlich. GitHub übernimmt die Sichtbarkeit des öffentlichen Repos, wenn die CI des Repos das Package anlegt. Anonym geprüft: alle drei per `docker pull` ohne Anmeldung abrufbar. Ein Klick ist also nicht nötig (F-018 erledigt).

## E-044 · Image-Scan mit Trivy, wöchentlicher Lauf
**Entscheidungen:**
- **Trivy im Job „Images und Smoke-Test“**, nach dem Smoke-Test, für alle drei Images. Der Bericht (hohe und kritische Lücken, für die es ein Update gibt) steht in der Zusammenfassung des Laufs. **Rot nur bei kritischen Lücken mit verfügbarem Update.** Lücken ohne Update kann niemand beheben. Ein Alarm dafür würde nur dazu erziehen, rote Läufe zu ignorieren.
- **Installation über `aquasecurity/setup-trivy` v0.3.1 und Trivy v0.76.2**, beides unveränderliche Releases, auf SHA bzw. Version festgenagelt. Hintergrund: Im März 2026 wurden 76 von 77 Tags der `trivy-action` auf Schadcode umgebogen, der Secrets aus CI-Läufen stahl (CVE-2026-33634). Genau dagegen helfen festgenagelte SHAs. Ein Tag wie `@v0.35` hätte damals den Schadcode geladen.
- **Wöchentlicher Lauf** (montags, `schedule`) und Start von Hand (`workflow_dispatch`): Neue Lücken werden bekannt, auch wenn sich am Code nichts ändert. Ein geplanter Lauf veröffentlicht keine Images, `publish` reagiert nur auf Pushes.
**Ergebnis beim Einführen:** alle drei Images 0 hohe und kritische Lücken. Trivy erkennt Ubuntu 24.04 auch in den chiseled-Images und prüft die .NET-Pakete über die `deps.json`.
**Grenze:** Das `efbundle` im Migrations-Image ist eine Single-File-Anwendung. Deren eingebettete Pakete sieht Trivy nicht. Sie sind dieselben wie in der Web-App, und die wird gescannt.

## E-008 · Privatsphäre im öffentlichen Repo
**Entscheidung (F-011):** Die Commits laufen über die anonyme GitHub-Adresse, `docs/AUFTRAG.md` bleibt nur lokal (`.gitignore`).
**Lernpunkt:** Die Git-Historie umzuschreiben (`git filter-branch` bzw. `git filter-repo`) ist nur unbedenklich, **solange noch nichts gepusht ist**. Danach hätten andere Kopien die alte Historie, und Daten, die einmal veröffentlicht sind, muss man als öffentlich betrachten.
