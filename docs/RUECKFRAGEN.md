# Rückfragen

So funktioniert es:
- Jede Frage hat eine Nummer (`F-001` …) und den Status **offen** oder **beantwortet**.
- Bei jeder Frage steht, **ab wann sie wichtig wird** und **was ich vorschlage**.
- Du schreibst deine Antwort direkt unter **Antwort:**. Ein „ok“ reicht, wenn du meinem Vorschlag zustimmst.
- Beantwortete Fragen bleiben hier stehen, damit der Verlauf nachvollziehbar ist. Was daraus folgt, steht in [ENTSCHEIDUNGEN.md](ENTSCHEIDUNGEN.md).

---

## Offen

*Zurzeit keine offenen Fragen.*

---

## Beantwortet

### F-001 · Der laufende Worker · beantwortet
**Kontext:** Dein Twitch-Worker läuft gerade in Docker (`twitch-worker-worker-1`, seit etwa 5 Stunden, `StopAfterHours=168`). Das ist vermutlich die einwöchige Marktstudie. Die Daten liegen im Volume `twitch-worker_pgdata`.
**Vorschlag:** Ich **kopiere** den Quellcode ins neue Repo und lasse `C:\Timo\Docker\twitch-worker` samt Containern völlig unangetastet, bis die Studie fertig ist. Die gesammelten Daten übernehmen wir später per `pg_dump` in die neue Datenbank, falls du sie behalten willst.
**Antwort (26.11.):** Ja, den Worker als eigenes Projekt mit ins Repo aufnehmen.

### F-002 · Echte Zugangsdaten in `.env.example` · beantwortet
**Kontext:** In `twitch-worker/.env.example` stehen Werte für `Twitch__ClientId`, `Twitch__ClientSecret` und `POSTGRES_PASSWORD`, die nach echten Daten aussehen. Die Beispieldatei ist dafür gedacht, ins Repo zu kommen.
**Vorschlag:** Im neuen Repo bekommt `.env.example` nur Platzhalter. Zusätzlich solltest du das Client Secret in der Twitch-Entwicklerkonsole **neu erzeugen**, falls die Datei je geteilt oder hochgeladen wurde. Das neue Secret kommt nur in die `.env`. Ich fasse die alten Dateien nicht an.
**Antwort (26.11.):** Ja, die `.env` kommt nicht ins Repo, sondern direkt in `.gitignore`.

### F-003 · Ort des Repos und GitHub · beantwortet
**Kontext:** Ich habe das Repo unter `C:\Timo\Docker\pubpot` angelegt, getrennt vom laufenden Worker. Für CI/CD brauchen wir später ein GitHub-Repo.
**Vorschlag:** Ein privates Repo `pubpot` unter deinem Konto. Hinweis: Manuelle Freigaben für Environments gibt es bei privaten Repos nur in kostenpflichtigen GitHub-Plänen, bei öffentlichen Repos sind sie kostenlos (siehe F-005).
**Antwort (26.11.):** Unter deinem eigenen Konto, **öffentlich**, auch als Showcase. Den Zugang klären wir über die GitHub CLI (siehe F-011).

### F-004 · .NET-10-SDK fehlt lokal · beantwortet
**Kontext:** Auf deinem Rechner sind die SDKs 9.0 und 11.0 installiert, aber kein 10.0. Baut lokal ein anderes SDK als in der CI, können sich `dotnet format` und die Analyzer unterschiedlich verhalten.
**Ursprünglicher Vorschlag:** Das .NET-10-SDK installieren und per `global.json` festlegen.
**Rückfrage (26.11.):** „Wäre es schlimm, es einfach mit 11 zu machen?“
**Erklärung:** Nachgeprüft: Die Laufzeit .NET 10 (10.0.15) ist bei dir installiert, nur das SDK 10 fehlt. Das ist kein Problem. Es gibt zwei Wege:
- **A (Vorschlag):** Die Projekte zielen weiter auf `net10.0` (LTS, wie im Auftrag), gebaut wird überall mit **SDK 11**: lokal, in der CI und im Docker-Build-Image. `global.json` legt SDK 11 fest, dadurch ist alles einheitlich. Ein SDK kann ältere Zielversionen problemlos bauen. Der einzige Nachteil ist ein kleiner Mehraufwand: Dependabot schlägt Paket-Updates auf 11.x vor, das unterbinde ich mit einer Regel. Die Laufzeit-Images bleiben bei `aspnet:10.0`.
- **B:** Komplett auf `net11.0` umsteigen. Seit .NET 9 hat auch die STS-Version (Standard Term Support) 24 Monate Support. .NET 11 und .NET 10 laufen deshalb beide etwa im November 2028 aus. Das wäre aber eine Abweichung vom Auftrag, und .NET 11 ist erst wenige Wochen alt.
**Antwort (26.11.):** Version A.

### F-005 · Manuelle Freigabe für Production · beantwortet
**Kontext:** Für Production verlangt der Auftrag eine manuelle Freigabe im GitHub-Environment „production“. Diese Funktion („Required reviewers“) gibt es für private Repos nur mit GitHub Team/Enterprise (kostet Geld).
**Optionen:** (a) Repo öffentlich machen: Freigabe kostenlos. (b) Privat bleiben und den Production-Deploy nur per Tag **und** manuell gestartetem Workflow (`workflow_dispatch`) erlauben. Das ist fast gleichwertig, aber ohne Freigabe-Button. (c) GitHub Team buchen.
**Antwort (26.11.):** Rückfrage „Was wäre die Alternative?“ Durch F-003 (öffentliches Repo) ist die Frage erledigt: Wir nutzen die echte, kostenlose Freigabe im Environment „production“, genau wie im Auftrag.

### F-006 · Datenbank des Workers: Schema per EF-Migration? · beantwortet
**Kontext:** Der Worker legt seine Tabellen beim Start selbst an (`CREATE TABLE IF NOT EXISTS`). Laut Auftrag laufen Migrationen als EF-Bundle vor dem App-Start. Der Worker nutzt aber gar kein EF, sondern direkt Npgsql mit `COPY`, und das ist für Massen-Inserts genau richtig.
**Vorschlag:** Web und Worker bekommen jeweils eine eigene Datenbank (`pubpot`, `twitch`) mit eigenem DB-Benutzer in derselben Postgres-Instanz. Das EF-Bundle gilt nur für die Web-App. Der Worker behält sein SQL-Schema als kleine versionierte SQL-Datei. Kein EF im Worker.
**Antwort (26.11.):** Getrennt, wie vorgeschlagen. → E-011

### F-007 · Externe Erreichbarkeitsprüfung · beantwortet
**Kontext:** Uptime Kuma läuft auf demselben VPS. Fällt der ganze Server aus, kann es dich nicht mehr warnen.
**Vorschlag:** Zusätzlich ein kostenloser externer Check (z. B. Healthchecks.io oder UptimeRobot), der nur `/health` von außen abfragt.
**Antwort (26.11.):** Nein, nur Uptime Kuma. → E-012

### F-008 · Kostenpflichtige Dienste · beantwortet (vorerst)
**Kontext:** Folgende Dinge kosten Geld. Ich führe sie nur nach deinem OK ein:
- Hetzner VPS: Empfehlung **CX23 oder vergleichbar (2 vCPU, 4 GB RAM)**, ca. 4–6 €/Monat.
- Backup-Ziel: Hetzner Storage Box BX11 (1 TB), ca. 3–4 €/Monat, oder S3-kompatibler Speicher.
- Domain `pubpot.games`: Preis je nach Registrar.
- SMTP-Anbieter in der EU: oft gibt es einen Gratis-Tarif mit wenigen hundert Mails am Tag.
**Antwort (26.11.):** Einen VPS gibt es noch nicht, den legst du an, sobald wir mit dem Deploy beginnen. Das Backup-Ziel entscheiden wir später, bis dahin gehen Backups in einen lokalen Ordner. → E-013. Die Frage kommt vor dem Deploy wieder auf den Tisch.

### F-009 · Zugang zum Monitoring · beantwortet
**Vorschlag:** Grafana, Prometheus und Uptime Kuma nur an `127.0.0.1` binden und per SSH-Tunnel erreichen. Die Alternative wäre Caddy mit Basic Auth.
**Antwort (26.11.):** SSH-Tunnel. Aber erst einmal soll alles zum Testen lokal auf deinem PC laufen und danach auf den VPS umziehen.
**Folgefrage (26.11.):** Monitoring, Alarme und Backups also lokal **vor** dem Deploy auf den VPS bauen? → **Ja.** → E-013

### F-010 · Alarm-Kanal · beantwortet
**Frage:** Discord-Webhook für Alarme und neue Anmeldungen, oder zunächst nur E-Mail?
**Antwort (26.11.):** Nur E-Mail. → E-014

### F-011 · GitHub-Zugang und öffentliches Repo · beantwortet
**Kontext:** Du hast angeboten, mir einen Schlüssel zu geben.
**Vorschlag:** Bitte **keinen Token in den Chat** kopieren. Stattdessen meldest du dich selbst über die GitHub CLI an.
**Bei einem öffentlichen Repo zu beachten:**
1. Die Commits enthalten deine E-Mail-Adresse. Vorschlag: die anonyme GitHub-Adresse verwenden, und zwar **vor dem ersten Push**.
2. `docs/AUFTRAG.md` (dein Geschäftskonzept inkl. Pilotplan) wäre öffentlich.
3. Was einmal gepusht ist, bleibt in der Git-Historie. Deshalb prüfe ich vor jedem Push auf Secrets und richte in Phase 6 zusätzlich GitHub Secret Scanning ein.
**Antwort (26.11.):** 1. Anonym klingt gut. 2. `AUFTRAG.md` gehört in `.gitignore`.
**Umgesetzt:** Für dieses Repo ist `54796926+TidragoUnity@users.noreply.github.com` eingestellt (gilt nur hier). Die Historie wurde vor dem ersten Push umgeschrieben. `gh` ist mit einem Fine-grained-Token angemeldet, der nur für `pubpot` gilt.

### F-012 · Texte der Website prüfen · beantwortet
**Kontext:** Die Texte der Startseite und der Anmeldeseiten habe ich aus dem Auftrag abgeleitet (Deutsch und Englisch).
**Offene Punkte, die ich bewusst nicht erfunden habe:** Ablauf des Pilots rund um das Next Fest, Definition „Steam-Nettoerlös“, Messung der „Reichweite“, Größenordnung im Rechenbeispiel, Marken-Hinweis in der Fußzeile.
**Antwort (26.11.):** Die Texte passen erst einmal so. Die fünf Punkte können bei Bedarf später ergänzt werden, spätestens bei der anwaltlichen Prüfung vor dem Go-live.

### F-013 · Wie lange bleiben bestätigte Anmeldungen gespeichert? · beantwortet
**Kontext:** Unbestätigte Anmeldungen löscht die App nach 30 Tagen automatisch, Mails nach 7 Tagen. Für **bestätigte** Anmeldungen gibt es noch keine Frist. Sie bleiben, bis jemand sie im Admin-Bereich löscht (Phase 4). Die DSGVO verlangt aber, Daten nur so lange zu speichern, wie es für den Zweck nötig ist.
**Mögliche Regeln:** (a) 12 Monate nach dem letzten Kontakt bzw. der letzten Änderung, (b) bis Ende des Pilots plus X Monate, (c) unbefristet bis zum Widerruf, das müsste aber in der Datenschutzerklärung stehen.
**Vorschlag:** (a) als Startpunkt. Technisch ist das eine Zeile im Aufräum-Job. Die Entscheidung gehört aber zu den Rechtstexten und sollte mit dem Anwalt abgestimmt werden.
**Antwort (26.11.):** Ein Jahr ohne Aktivität klingt gut. Dann eine Mail, dass die Daten in den nächsten 30 Tagen gelöscht werden, falls keine Rückmeldung kommt. Und eine Mail, sobald man die Löschung verhindert hat, damit man weiß, dass es geklappt hat.
**Rückfragen dazu (26.11.):**
- Was zählt als Aktivität? → **Nur der Link „Ich möchte dabei bleiben“ in der Warnmail.** Ein erneut abgeschicktes Formular zählt nicht.
- Zählt ein aktiver Admin-Status? → **Ja.** Die Status „kontaktiert“, „Gespräch“ und „Pilot“ bzw. eine Änderung im Admin-Bereich setzen die Frist zurück.
- Wann bauen? → **Nach Phase 4**, weil der Admin-Status hineinspielt. → E-023

### F-014 · Admin-Bereich nur auf Deutsch? · beantwortet
**Wichtig ab:** Schritt 4.2.
**Kontext:** Die Regel lautet bisher: Alle Texte stehen in `.resx`, auf Deutsch und Englisch. Den Admin-Bereich nutzt aber nur eine Person, nämlich du.
**Vorschlag:** Der Admin-Bereich ist **nur auf Deutsch**, die Texte stehen direkt in den Razor-Seiten und er hat keine `/de`- oder `/en`-Route. Das spart etwa ein Drittel der Arbeit in Phase 4. Der Paritätstest bleibt für die öffentlichen Seiten wie gehabt.
**Antwort (26.11.):** Ja, Deutsch reicht.

### F-015 · Zählt der Status „Pilot“ dauerhaft als Aktivität? · beantwortet
**Wichtig ab:** Schritt 4.9.
**Kontext:** Laut E-023 zählen ein aktiver Admin-Status („kontaktiert“, „Gespräch“, „Pilot“) oder eine Änderung im Admin-Bereich als Aktivität. Unklar ist, ob ein Eintrag, der seit 12 Monaten unverändert auf „Pilot“ steht, trotzdem eine Warnmail bekommt.
**Möglichkeiten:** (a) Nur die **Änderung** zählt. Wer 12 Monate nichts ändert, bekommt die Warnmail, egal welcher Status. (b) Solange ein aktiver Status gesetzt ist, gibt es **nie** eine Warnmail.
**Vorschlag:** (a). Das ist eindeutig, die Frist hängt nicht davon ab, ob du vergisst, einen Status zurückzusetzen, und eine kurze Notiz reicht, um die 12 Monate neu zu starten.
**Antwort (26.11.):** Ja, auch „Pilot“ bekommt die Warnmail.

### F-016 · Worker lokal neben der laufenden Studie · beantwortet
**Wichtig ab:** Schritt 5.4.
**Kontext:** Deine Studie läuft noch bis etwa 03.12. mit deinen Twitch-Zugangsdaten. Startet der Worker aus `compose.yaml` mit denselben Daten, laufen zwei Worker gegen dasselbe Twitch-Limit (pro Client-ID). Das kann die Studie stören. Ohne Zugangsdaten startet der Worker gar nicht (er prüft sie beim Start).
**Möglichkeiten:** (a) Der Worker hat ein **Compose-Profil** `worker`. `docker compose up` startet ihn nicht, `docker compose --profile worker up` schon. Auf dem VPS wird das Profil per `COMPOSE_PROFILES=worker` in der `.env` eingeschaltet. (b) Der Worker startet immer mit, und du trägst bis zum Ende der Studie keine Zugangsdaten ein (Container startet dann ständig neu).
**Vorschlag:** (a). Die Abnahme in 5.8 prüft beim Worker dann Image, Schema-Skript und Healthcheck, aber keine echte Runde gegen Twitch. Eine echte Runde machen wir, wenn die Studie fertig ist oder du eine zweite Twitch-App (kostenlos) für Tests anlegst.
**Antwort (27.11.):** Planung passt, also Vorschlag (a): Profil `worker`.

### F-017 · HTTPS lokal mit Caddy · beantwortet
**Wichtig ab:** Schritt 5.6.
**Kontext:** Lokal kann Caddy kein Let's-Encrypt-Zertifikat für `localhost` holen. Er stellt sich dann selbst eins aus (eigene Zertifizierungsstelle). Der Browser warnt davor, bis du Caddys Stammzertifikat in Windows als vertrauenswürdig einträgst (ein Befehl, jederzeit wieder entfernbar).
**Möglichkeiten:** (a) Lokal **HTTPS** unter `https://localhost`, Zertifikat vertrauen ist optional und steht als Anleitung in der README. (b) Lokal nur **HTTP** über Caddy. Dann lassen sich HSTS, Secure-Cookies und das Admin-Cookie (`__Host-`) lokal nicht so testen wie auf dem Server.
**Vorschlag:** (a). Lokal läuft dann dasselbe wie später auf dem VPS, nur mit anderem Zertifikat. Ob du das Zertifikat einträgst, entscheidest du selbst. Ohne Eintrag klickst du einmal „Trotzdem fortfahren“.
**Antwort (27.11.):** HTTPS, die Zertifikatswarnung klicke ich vorerst weg.

### F-018 · Images in GHCR öffentlich oder privat? · beantwortet
**Wichtig ab:** Schritt 6.3.
**Kontext:** Die CI legt die Images in der GitHub Container Registry ab. Neue Images sind dort zunächst privat. Der VPS bräuchte dann ein eigenes Token zum Herunterladen.
**Möglichkeiten:** (a) **öffentlich:** jeder kann die Images ziehen, kein Token auf dem Server nötig. In den Images steckt nur, was ohnehin im öffentlichen Repo liegt, keine Secrets. (b) **privat:** nur du, der VPS braucht ein Token mit `read:packages`, das alle 90 Tage erneuert werden muss.
**Vorschlag:** (a). Das Repo ist ohnehin öffentlich, die Images verraten nichts Neues, und auf dem Server liegt ein Geheimnis weniger. Umstellen geht einmalig per Klick auf GitHub (Package settings → Change visibility). Das steht dann in der Checkliste.
**Antwort (27.11.):** Ja, öffentlich, wenn es kein Aufwand ist (sonst wäre auch das Token in Ordnung). → Einmal ein Klick pro Image nach dem ersten Push, also öffentlich.

### F-019 · Arbeitsweise mit Pull Requests ab Schritt 6.7 · beantwortet
**Wichtig ab:** Schritt 6.7.
**Kontext:** Bisher committe ich direkt auf `main`. Mit den Regeln aus 6.7 geht das nur noch über Pull Requests mit grüner CI. Das ist der eigentliche Sinn der CI: Nichts kommt ungeprüft auf `main`.
**Möglichkeiten:** (a) Pro Roadmap-Schritt ein Branch und ein PR. Ist die CI grün, merge ich per „Squash and merge“. Auf `main` bleibt ein Commit `x.y: …` pro Schritt wie bisher. Du kannst jeden PR vorher auf GitHub ansehen. (b) Wie (a), aber **du** mergst jeden PR selbst. (c) Weiter direkt auf `main`, mit einer Ausnahme für dich in den Regeln.
**Vorschlag:** (a). So lernst du den üblichen Ablauf kennen, die CI hat das letzte Wort, und du musst nicht bei jedem Schritt klicken. Hinweis: Jeder Push braucht im Moment deine Freigabe. Wenn du willst, erlaube ich in den Einstellungen `git push` auf Branches außer `main`.
**Antwort (27.11.):** Genau, jedes Feature entsteht in einem eigenen Branch und wird dann auf `main` gemergt. → Vorschlag (a), ab 6.1.

### F-020 · Prometheus-Endpunkt: Paket noch in der Vorschau · beantwortet
**Wichtig ab:** Schritt 7.2.
**Kontext:** Der Auftrag verlangt „Metriken über OpenTelemetry mit Prometheus-Endpunkt“. OpenTelemetry selbst ist stabil (1.21.0). Das Paket für den Prometheus-Endpunkt (`OpenTelemetry.Exporter.Prometheus.AspNetCore`) gibt es aber nur als Vorschau (`1.21.0-beta.1`), schon seit Jahren. Es kommt vom OpenTelemetry-Projekt selbst und ist weit verbreitet. „Beta“ heißt vor allem, dass sich die Schnittstelle bei einem Update noch ändern darf.
**Möglichkeiten:** (a) **Das Vorschau-Paket**, genau wie im Auftrag: Prometheus holt die Werte regelmäßig ab („Pull“). Fällt die App aus, merkt Prometheus das sofort (`up = 0`). (b) **OTLP statt Prometheus-Endpunkt:** Die App schickt die Werte mit dem stabilen OTLP-Exporter an Prometheus, das sie seit Version 3 direkt annimmt („Push“). Alles stabil, aber ohne `/metrics`-Endpunkt, und einen Ausfall erkennt man nur indirekt („keine Daten mehr“).
**Vorschlag:** (a). Sie entspricht dem Auftrag und dem üblichen Aufbau, den du später überall wiederfindest. Das Risiko ist klein: Ändert sich bei einem Update etwas, fällt es in der CI auf (Phase 6).
**Antwort (27.11.):** Ja, passt, das Vorschau-Paket nehmen. → (a)

### F-021 · Erreichbarkeit: Uptime Kuma oder Blackbox-Exporter? · beantwortet
**Wichtig ab:** Schritt 7.5.
**Kontext:** Der Auftrag nennt „Uptime Kuma (oder vergleichbar)“ für die Erreichbarkeit der Website und als Push-Monitor für den Worker. Uptime Kuma hat eine schöne Oberfläche, wird aber **nur dort eingerichtet**, per Klick. Es gibt keine offizielle Konfiguration als Datei. Außerdem hat es eigene Benachrichtigungen, also gäbe es zwei Stellen, an denen Alarme entstehen.
**Möglichkeiten:** (a) **Uptime Kuma:** Container im Profil `monitoring`, du richtest die zwei Monitore und die Mail-Benachrichtigung einmal nach meiner Anleitung ein (etwa 5 Minuten). Die Einstellungen liegen in seinem Volume. (b) **Blackbox-Exporter von Prometheus:** prüft die Website über Caddy (Status, HTTPS, Zertifikat). Den Worker überwacht Prometheus über seine Metriken aus 7.3. Alles steht als Datei im Repo, ist getestet und läuft über **einen** Alarmweg (Alertmanager). Die Push-URL des Workers gibt es trotzdem, für einen externen Dienst in Phase 8.
**Vorschlag:** (b). Alles als Code, nichts muss von Hand nachgebaut werden, wenn ein Volume verloren geht, und alle Alarme kommen aus einer Quelle. Wenn dir eine Status-Oberfläche wie bei Kuma wichtig ist, nehmen wir (a).
**Antwort (27.11.):** Klingt gut, der Blackbox-Exporter gefällt mir. → (b)
