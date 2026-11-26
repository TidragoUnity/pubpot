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
**Noch offen (Phase 5):** HSTS und `upgrade-insecure-requests` greifen nur, wenn die App HTTPS erkennt. Hinter Caddy kommt das über `X-Forwarded-Proto`, dafür braucht es die Forwarded-Headers-Middleware.
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

**Offen für Phase 5:** Hinter Caddy sieht die App ohne Forwarded Headers nur die IP von Caddy. Alle Besucher würden sich dann ein Rate-Limit teilen.

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

## E-008 · Privatsphäre im öffentlichen Repo
**Entscheidung (F-011):** Die Commits laufen über die anonyme GitHub-Adresse, `docs/AUFTRAG.md` bleibt nur lokal (`.gitignore`).
**Lernpunkt:** Die Git-Historie umzuschreiben (`git filter-branch` bzw. `git filter-repo`) ist nur unbedenklich, **solange noch nichts gepusht ist**. Danach hätten andere Kopien die alte Historie, und Daten, die einmal veröffentlicht sind, muss man als öffentlich betrachten.
