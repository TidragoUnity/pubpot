# Twitch-Worker für die Marktstudie

Fragt alle paar Minuten alle laufenden Twitch-Streams ab (standardmäßig alle deutschsprachigen),
speichert Zuschauerzahl, Kategorie und Kanal in PostgreSQL und liefert eine fertige Auswertung:
Wie viele Kanäle haben im Schnitt 3, 10, 50 oder 100 Zuschauer, und welche Spiele streamen sie?

Später lässt sich derselbe Worker für die Plattform nutzen: statt aller deutschen Streams nur die
Kanäle der Teilnehmer (`Worker__UserLogins`).

## Aufbau

| Datei | Aufgabe |
|---|---|
| `Program.cs` | Start, Einstellungen, Verdrahtung |
| `TwitchClient.cs` | Zugangsschlüssel holen, `Get Streams` durchblättern, Rate-Limit und Fehler behandeln |
| `SnapshotWorker.cs` | Die Schleife: alle X Minuten abfragen, Doppelte entfernen, speichern, Heartbeat |
| `SampleStore.cs` | Prüfen, ob die Tabellen da sind, und Runden per `COPY` in PostgreSQL schreiben |
| `sql/analysis.sql` | Auswertung: Verteilung, Zielgruppe, Kategorien, Punkte pro Stream, CSV-Export |
| `HealthCheckCommand.cs` | Docker-Healthcheck: Heartbeat-Datei höchstens 15 Minuten alt |
| `../../deploy/db/twitch/` | Schema als SQL-Skripte (`001_schema.sql`) und der Runner `migrate.sh` |

Die Datenbank hat zwei Tabellen:

- `snapshots`: eine Zeile pro Abfrage-Runde (Zeitpunkt, Anzahl Streams, Dauer, vollständig ja/nein)
- `stream_samples`: eine Zeile pro Stream und Runde (Kanal, Stream-ID, Kategorie, Zuschauer)

## 1. Twitch-App anlegen

1. Auf https://dev.twitch.tv/console/apps mit deinem Twitch-Konto anmelden. Twitch verlangt dafür
   aktivierte Zwei-Faktor-Anmeldung.
2. „Anwendung registrieren“: beliebiger Name, als OAuth-Redirect `http://localhost`, Kategorie z. B.
   „Analytics Tool“, Client-Typ „Vertraulich“.
3. Danach die **Client-ID** kopieren und über „Neues Geheimnis“ das **Client-Secret** erzeugen.
   Das Secret wird nur einmal angezeigt und gehört nie in Git oder in den Code.

## 2. Starten mit Docker

Der Worker läuft mit in der gemeinsamen Compose-Datei `deploy/compose.yaml`, mit eigener Datenbank `twitch`
und eigenem DB-Benutzer (E-011). Er startet nur mit dem **Profil `worker`** (E-032), damit ein einfaches
`docker compose up` ihn nicht versehentlich mitstartet.

```bash
cd deploy
# In deploy/.env eintragen: Twitch__ClientId, Twitch__ClientSecret (Vorlage: .env.example)
docker compose --profile worker up -d --build
docker compose --profile worker logs -f worker
```

Vor dem Worker läuft einmal der Container `migrate-twitch`. Er spielt die SQL-Skripte aus
`deploy/db/twitch` ein, der Worker legt keine Tabellen mehr selbst an. Auf dem Server schaltet
`COMPOSE_PROFILES=worker` in der `.env` das Profil dauerhaft ein.

Im Log sollte nach wenigen Sekunden so etwas stehen:

```
Neuer Zugangsschlüssel, gültig bis ...
Runde gespeichert: 4812 Streams, 163204 Zuschauer, 49 Seiten, 18.3s.
```

`docker compose ps` zeigt beim Worker nach ein paar Minuten `healthy`. Wird er `unhealthy`, hat er
seit 15 Minuten keine Runde gespeichert, dann im Log nachsehen.

Im DB-Passwort keine Semikolons verwenden, sonst bricht die Verbindungszeichenkette.

## 3. Einstellungen

Alle Werte stehen in `deploy/.env` (Umgebungsvariablen überschreiben `appsettings.json`). Compose reicht
sie nur an den Worker weiter, die Web-App sieht sie nicht:

| Variable | Standard | Bedeutung |
|---|---|---|
| `Worker__IntervalMinutes` | 5 | Abstand zwischen zwei Runden |
| `Worker__Languages` | de | Sprachen, kommagetrennt |
| `Worker__GameIds` | leer | Nur diese Kategorien (Twitch-Game-IDs) |
| `Worker__UserLogins` | leer | Nur diese Kanäle; ersetzt den Sprachfilter |
| `Worker__StopAfterHours` | 0 | Nach so vielen Stunden ab der ersten Runde aufhören (168 = eine Woche, 0 = endlos) |

Nach Ablauf von `StopAfterHours` läuft der Container weiter, fragt aber nichts mehr ab. Auch nach
einem Neustart zählt die Zeit ab der allerersten Runde, die Messung beginnt also nicht von vorn.

## 4. Auswerten

Jederzeit möglich, auch während die Messung noch läuft. Lokal hängt `compose.override.yaml` die Ordner
`sql/` und `export/` in den Datenbank-Container ein:

```bash
cd deploy
docker compose exec db psql -U twitch -d twitch -f /sql/analysis.sql
```

Das Skript zeigt:

1. Überblick: Zeitraum, Streams und Zuschauer gleichzeitig, Zahl der Kanäle
2. Lücken in der Messung (z. B. wenn der Server neu gestartet wurde)
3. Verteilung der Kanäle nach durchschnittlichen Zuschauern, getrennt nach allen, Gaming und
   Gaming mit mindestens 3 Streams im Zeitraum
4. Kanäle mit mindestens 3, 10, 50 und 100 Zuschauern
5. Die häufigsten Spiele bei deiner Zielgruppe (Gaming, 3 bis unter 50 Zuschauer)
6. Punkte pro Stream nach deiner Pool-Regel (Ø-Zuschauer × Stunden × 30), nur Spiele ab 1 Stunde

Außerdem schreibt es `export/kanaele.csv` (ohne Kanalnamen) für Tabellen und Diagramme.
Hast du ein anderes Intervall als 5 Minuten eingestellt, oben im Skript `\set intervall_min` anpassen.
Welche Kategorien nicht als Spiel zählen (Just Chatting, Slots, Musik usw.), steht in der Liste
`keine_spiele` am Anfang und lässt sich ergänzen.

Schnelle Kontrolle zwischendurch:

```bash
docker compose exec db psql -U twitch -d twitch -c \
  "SELECT taken_at, stream_count, viewer_sum, complete FROM snapshots ORDER BY id DESC LIMIT 5"
```

## 5. Gut zu wissen

- **Speicherplatz:** Bei rund 5.000 deutschen Streams und 5-Minuten-Takt kommen pro Woche etwa
  10 Millionen Zeilen zusammen, grob 1–2 GB. Für einen VPS mit 40 GB kein Problem. Mit 10 Minuten
  Takt halbiert sich das.
- **Genauigkeit:** Twitch sortiert nach Zuschauern. Während der Worker blättert, verschieben sich
  Streams, dadurch tauchen manche doppelt auf (werden entfernt) oder fehlen in einer Runde. Über eine
  Woche mittelt sich das heraus.
- **Rate-Limit:** Twitch begrenzt die Anfragen pro Minute. Der Worker liest die Kopfzeilen
  `Ratelimit-Remaining` und `Ratelimit-Reset` und wartet bei Bedarf. Eine Runde mit 50 Seiten liegt
  weit unter dem Limit.
- **Fehler:** Netzwerkfehler und 500er werden bis zu fünfmal wiederholt, ein abgelaufener Schlüssel
  wird automatisch erneuert. Scheitert eine Runde trotzdem, wird sie als unvollständig markiert und
  in der Auswertung ignoriert.
- **Datenschutz:** Gespeichert werden nur öffentliche Daten, aber Kanalnamen sind personenbezogen.
  Für die Studie nur zusammengefasste Zahlen weitergeben (der CSV-Export enthält keine Namen) und die
  Rohdaten nach der Auswertung löschen, und zwar nur die Tabellen des Workers:
  `docker compose exec db psql -U twitch -d twitch -c "TRUNCATE snapshots CASCADE"`.
  **Nicht** `docker compose down -v`: Das löscht das gemeinsame Volume und damit auch alle PubPot-Anmeldungen.
- **Datenbank nicht öffentlich:** In `deploy/compose.yaml` ist bewusst kein Port freigegeben. Nur lokal
  gibt `compose.override.yaml` den Port an `127.0.0.1:5432` frei, für Datenbank-Werkzeuge und `dotnet run`.

## 6. Ohne Docker entwickeln (z. B. in Rider oder Visual Studio)

Du brauchst das .NET SDK 11 (Version siehe `global.json` im Repo-Wurzelordner, Ziel ist .NET 10) und eine erreichbare PostgreSQL-Datenbank. Das Schema einmal einspielen (`cd deploy && docker compose --profile worker run --rm migrate-twitch`), dann im Projektordner:

```bash
dotnet user-secrets init
dotnet user-secrets set "Twitch:ClientId" "..."
dotnet user-secrets set "Twitch:ClientSecret" "..."
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=twitch;Username=twitch;Password=..."
dotnet run --environment Development
```

User-Secrets liegen außerhalb des Projektordners und landen dadurch nicht versehentlich in Git.
