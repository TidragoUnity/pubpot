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
| `SampleStore.cs` | Tabellen anlegen und Runden per `COPY` in PostgreSQL schreiben |
| `sql/analysis.sql` | Auswertung: Verteilung, Zielgruppe, Kategorien, Punkte pro Stream, CSV-Export |
| `docker-compose.yml` | Datenbank und Worker zusammen starten |

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

Voraussetzung: Docker mit Compose (lokal oder auf dem VPS).

```bash
cp .env.example .env      # dann Client-ID, Secret und ein DB-Passwort eintragen
docker compose up -d --build
docker compose logs -f worker
```

Im Log sollte nach wenigen Sekunden so etwas stehen:

```
Neuer Zugangsschlüssel, gültig bis ...
Runde gespeichert: 4812 Streams, 163204 Zuschauer, 49 Seiten, 18.3s.
```

`docker compose ps` zeigt beim Worker nach ein paar Minuten `healthy`. Wird er `unhealthy`, hat er
seit 15 Minuten keine Runde gespeichert, dann im Log nachsehen.

Im DB-Passwort keine Semikolons verwenden, sonst bricht die Verbindungszeichenkette.

## 3. Einstellungen

Alle Werte stehen in `.env` (Umgebungsvariablen überschreiben `appsettings.json`):

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

Jederzeit möglich, auch während die Messung noch läuft:

```bash
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
  Rohdaten nach der Auswertung löschen: `docker compose down -v` entfernt Container und Datenbank.
- **Datenbank nicht öffentlich:** In `docker-compose.yml` ist bewusst kein Port freigegeben. Willst du
  lokal mit einem Datenbank-Tool darauf zugreifen, beim Dienst `db` vorübergehend
  `ports: ["127.0.0.1:5432:5432"]` ergänzen, auf dem VPS nie ohne `127.0.0.1`.

## 6. Ohne Docker entwickeln (z. B. in Rider oder Visual Studio)

Du brauchst das .NET 8 SDK und eine erreichbare PostgreSQL-Datenbank. Dann im Projektordner:

```bash
dotnet user-secrets init
dotnet user-secrets set "Twitch:ClientId" "..."
dotnet user-secrets set "Twitch:ClientSecret" "..."
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=twitch;Username=twitch;Password=..."
dotnet run --environment Development
```

User-Secrets liegen außerhalb des Projektordners und landen dadurch nicht versehentlich in Git.
