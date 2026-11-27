# Journal

Chronologisches Protokoll: was gemacht wurde, was als Nächstes kommt. Neueste Einträge oben.
Damit kann jede neue Arbeitssitzung nahtlos weitermachen.

---

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
