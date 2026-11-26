# Rückfragen

So funktioniert es:
- Jede Frage hat eine Nummer (`F-001` …) und den Status **offen** oder **beantwortet**.
- Bei jeder Frage steht, **ab wann sie wichtig wird** und **was ich vorschlage**.
- Du schreibst deine Antwort direkt unter **Antwort:**. Ein „ok“ reicht, wenn du meinem Vorschlag zustimmst.
- Beantwortete Fragen bleiben hier stehen, damit der Verlauf nachvollziehbar ist. Was daraus folgt, steht in [ENTSCHEIDUNGEN.md](ENTSCHEIDUNGEN.md).

---

## Offen

### F-013 · Wie lange bleiben bestätigte Anmeldungen gespeichert? · offen (vor dem Go-live)
**Kontext:** Unbestätigte Anmeldungen löscht die App nach 30 Tagen automatisch, Mails nach 7 Tagen. Für **bestätigte** Anmeldungen gibt es noch keine Frist. Sie bleiben, bis jemand sie im Admin-Bereich löscht (Phase 4). Die DSGVO verlangt aber, Daten nur so lange zu speichern, wie es für den Zweck nötig ist.
**Mögliche Regeln:** (a) 12 Monate nach dem letzten Kontakt bzw. der letzten Änderung, (b) bis Ende des Pilots plus X Monate, (c) unbefristet bis zum Widerruf, das müsste aber in der Datenschutzerklärung stehen.
**Vorschlag:** (a) als Startpunkt. Technisch ist das eine Zeile im Aufräum-Job. Die Entscheidung gehört aber zu den Rechtstexten und sollte mit dem Anwalt abgestimmt werden.
**Antwort:**

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
