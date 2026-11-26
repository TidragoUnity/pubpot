# Rückfragen

So funktioniert es:
- Jede Frage hat eine Nummer (`F-001` …) und den Status **offen** oder **beantwortet**.
- Bei jeder Frage steht, **ab wann sie wichtig wird** und **was ich vorschlage**.
- Du schreibst deine Antwort direkt unter **Antwort:**. Ein „ok“ reicht, wenn du meinem Vorschlag zustimmst.
- Beantwortete Fragen bleiben hier stehen, damit der Verlauf nachvollziehbar ist. Was daraus folgt, steht in [ENTSCHEIDUNGEN.md](ENTSCHEIDUNGEN.md).

---

## Offen – wichtig vor Phase 1

### F-001 · Der laufende Worker · offen
**Kontext:** Dein Twitch-Worker läuft gerade in Docker (`twitch-worker-worker-1`, seit etwa 5 Stunden, `StopAfterHours=168`). Das ist vermutlich die einwöchige Marktstudie. Die Daten liegen im Volume `twitch-worker_pgdata`.
**Vorschlag:** Ich **kopiere** den Quellcode ins neue Repo und lasse `C:\Timo\Docker\twitch-worker` samt Containern völlig unangetastet, bis die Studie fertig ist. Die gesammelten Daten übernehmen wir später per `pg_dump` in die neue Datenbank, falls du sie behalten willst.
**Antwort:**

### F-002 · Echte Zugangsdaten in `.env.example` · offen
**Kontext:** In `twitch-worker/.env.example` stehen Werte für `Twitch__ClientId`, `Twitch__ClientSecret` und `POSTGRES_PASSWORD`, die nach echten Daten aussehen. Die Beispieldatei ist dafür gedacht, ins Repo zu kommen.
**Vorschlag:** Im neuen Repo bekommt `.env.example` nur Platzhalter. Zusätzlich solltest du das Client Secret in der Twitch-Entwicklerkonsole **neu erzeugen**, falls die Datei je geteilt oder hochgeladen wurde. Das neue Secret kommt nur in die `.env`. Ich fasse die alten Dateien nicht an.
**Antwort:**

### F-003 · Ort des Repos und GitHub · offen
**Kontext:** Ich habe das Repo unter `C:\Timo\Docker\pubpot` angelegt, getrennt vom laufenden Worker. Für CI/CD brauchen wir später ein GitHub-Repo.
**Vorschlag:** Lokal bleibt es bei `C:\Timo\Docker\pubpot`. Auf GitHub ein **privates** Repo `pubpot` unter deinem Konto. Für ein privates Repo sind Actions (2.000 Minuten im Monat) und GHCR im Rahmen der Freikontingente kostenlos. Das reicht für dieses Projekt. Wichtig: Manuelle Freigaben für Environments gibt es bei **privaten** Repos nur in kostenpflichtigen GitHub-Plänen. Mit einem **öffentlichen** Repo bekommst du sie kostenlos (siehe F-005).
**Antwort:** (Konto- oder Organisationsname? privat oder öffentlich?)

### F-004 · .NET-10-SDK fehlt lokal · offen
**Kontext:** Auf deinem Rechner sind die SDKs 9.0 und 11.0 installiert, aber kein 10.0. Das SDK 11 kann zwar `net10.0` bauen. In der CI läuft aber SDK 10, und Unterschiede bei `dotnet format` oder den Analyzern würden lokal grüne Builds in der CI rot machen.
**Vorschlag:** Du installierst das .NET-10-SDK (`winget install Microsoft.DotNet.SDK.10`). Ich lege per `global.json` SDK 10 fest, damit lokal und in der CI dieselbe Version baut.
**Antwort:**

---

## Offen – wichtig ab späteren Phasen (kein Zeitdruck)

### F-005 · Manuelle Freigabe für Production (Phase 7) · offen
**Kontext:** Für Production verlangt der Auftrag eine manuelle Freigabe im GitHub-Environment „production“. Diese Funktion („Required reviewers“) gibt es für private Repos nur mit GitHub Team/Enterprise (kostet Geld).
**Optionen:** (a) Repo öffentlich machen: Freigabe kostenlos, Secrets bleiben trotzdem geheim. (b) Privat bleiben und den Production-Deploy nur per Tag **und** manuell gestartetem Workflow (`workflow_dispatch`) erlauben. Das ist fast gleichwertig, aber ohne Freigabe-Button. (c) GitHub Team buchen.
**Vorschlag:** (b), außer du willst das Repo ohnehin öffentlich machen.
**Antwort:**

### F-006 · Datenbank des Workers: Schema per EF-Migration? (Phase 5) · offen
**Kontext:** Der Worker legt seine Tabellen beim Start selbst an (`CREATE TABLE IF NOT EXISTS`). Laut Auftrag laufen Migrationen als EF-Bundle vor dem App-Start. Der Worker nutzt aber gar kein EF, sondern direkt Npgsql mit `COPY`, und das ist für Massen-Inserts genau richtig.
**Vorschlag:** Web und Worker bekommen jeweils eine eigene Datenbank (`pubpot`, `twitch`) mit eigenem DB-Benutzer in derselben Postgres-Instanz. Das EF-Bundle gilt nur für die Web-App. Der Worker behält sein idempotentes SQL-Schema, umgestellt auf eine kleine versionierte SQL-Datei. Kein EF im Worker.
**Antwort:**

### F-007 · Externe Erreichbarkeitsprüfung (Phase 8) · offen
**Kontext:** Uptime Kuma läuft auf demselben VPS. Fällt der ganze Server aus, kann es dich nicht mehr warnen.
**Vorschlag:** Uptime Kuma wie geplant, dazu **ein** kostenloser externer Check (z. B. Healthchecks.io oder UptimeRobot im Gratis-Tarif), der nur `/health` von außen abfragt. Das ist ein Drittanbieter, deshalb frage ich vorher.
**Antwort:**

### F-008 · Kostenpflichtige Dienste (Phase 7/8) · offen
**Kontext:** Folgende Dinge kosten Geld. Ich führe sie nur nach deinem OK ein:
- Hetzner VPS: Empfehlung **CX23 oder vergleichbar (2 vCPU, 4 GB RAM)**, ca. 4–6 €/Monat. Postgres, Web, Worker, Prometheus und Grafana brauchen zusammen etwa 1,5–2,5 GB RAM.
- Backup-Ziel: Hetzner Storage Box BX11 (1 TB), ca. 3–4 €/Monat. Alternative: S3-kompatibler Speicher.
- Domain `pubpot.games`: Preis je nach Registrar.
- SMTP-Anbieter in der EU: oft gibt es einen Gratis-Tarif mit wenigen hundert Mails am Tag.
**Antwort:** (Gibt es den VPS schon? Welches Backup-Ziel?)

### F-009 · Zugang zum Monitoring (Phase 8) · offen
**Vorschlag:** Grafana, Prometheus und Uptime Kuma werden nur an `127.0.0.1` auf dem VPS gebunden. Du erreichst sie per SSH-Tunnel (`ssh -L 3000:localhost:3000 …`). Das ist am sichersten und braucht keine weitere Subdomain und kein weiteres Passwort im Internet. Die Alternative wäre Caddy mit Basic Auth.
**Antwort:**

### F-010 · Alarm-Kanal (Phase 4 + 8) · offen
**Frage:** Hast du einen Discord-Server, auf dem ich einen Webhook für Alarme und neue Anmeldungen nutzen kann, oder zunächst nur E-Mail?
**Antwort:**
