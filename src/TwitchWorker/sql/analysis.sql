-- Auswertung der Marktstudie
-- Ausführen:  docker compose exec db psql -U twitch -d twitch -f /sql/analysis.sql
--
-- "Durchschnittliche Zuschauer" eines Kanals = Mittelwert aller Abfragen, in denen er live war.
-- Weil die Abfragen gleichmäßig verteilt sind, entspricht das der üblichen Definition
-- (Zuschauerstunden geteilt durch gestreamte Stunden), wie sie auch TwitchTracker nutzt.

-- An Worker__IntervalMinutes anpassen:
\set intervall_min 5
\pset footer off
\pset null '-'

-- Kategorien, die keine Spiele sind. Bei Bedarf ergänzen.
CREATE TEMP TABLE keine_spiele (game_name text PRIMARY KEY);
INSERT INTO keine_spiele VALUES
  (''), ('Just Chatting'), ('IRL'), ('Music'), ('Art'), ('Talk Shows & Podcasts'),
  ('Sports'), ('Slots'), ('Virtual Casino'), ('Pools, Hot Tubs, and Beaches'), ('ASMR'),
  ('Travel & Outdoors'), ('Science & Technology'), ('Software and Game Development'),
  ('Makers & Crafting'), ('Food & Drink'), ('Fitness & Health'), ('Politics'),
  ('Special Events'), ('Beauty & Body Art'), ('Animals, Aquariums, and Zoos'),
  ('Crypto'), ('Writing & Reading'), ('Co-working & Studying'), ('Dance'), ('DJs');

-- Nur vollständige Runden auswerten.
CREATE TEMP VIEW proben AS
SELECT ss.*, (ss.game_name NOT IN (SELECT game_name FROM keine_spiele)) AS ist_spiel
FROM stream_samples ss
JOIN snapshots sn ON sn.id = ss.snapshot_id
WHERE sn.complete;

-- Eine Zeile pro Kanal.
CREATE TEMP TABLE kanaele AS
WITH haupt AS (
  SELECT DISTINCT ON (user_id) user_id, game_name AS hauptkategorie
  FROM proben
  GROUP BY user_id, game_name
  ORDER BY user_id, count(*) DESC, game_name
)
SELECT p.user_id,
       max(p.user_login)                                  AS login,
       h.hauptkategorie,
       round(avg(p.viewer_count), 2)                      AS avg_zuschauer,
       count(DISTINCT p.stream_id)                        AS streams,
       round(count(*) * :intervall_min / 60.0, 1)         AS stunden_live,
       round(avg(p.ist_spiel::int), 2)                    AS anteil_spiele
FROM proben p
JOIN haupt h USING (user_id)
GROUP BY p.user_id, h.hauptkategorie;

CREATE TEMP VIEW stufen AS
SELECT k.*,
       anteil_spiele >= 0.5                    AS gaming,
       (anteil_spiele >= 0.5 AND streams >= 3) AS gaming_regelmaessig,
       CASE
         WHEN avg_zuschauer <   1 THEN 1
         WHEN avg_zuschauer <   3 THEN 2
         WHEN avg_zuschauer <   5 THEN 3
         WHEN avg_zuschauer <  10 THEN 4
         WHEN avg_zuschauer <  25 THEN 5
         WHEN avg_zuschauer <  50 THEN 6
         WHEN avg_zuschauer < 100 THEN 7
         ELSE 8
       END AS stufe
FROM kanaele k;

\echo
\echo '=== 1. Überblick ==='
SELECT count(*)                                  AS runden,
       count(*) FILTER (WHERE NOT complete)      AS unvollstaendig,
       to_char(min(taken_at), 'YYYY-MM-DD HH24:MI') AS von,
       to_char(max(taken_at), 'YYYY-MM-DD HH24:MI') AS bis,
       round(avg(stream_count) FILTER (WHERE complete)) AS streams_gleichzeitig,
       round(avg(viewer_sum)   FILTER (WHERE complete)) AS zuschauer_gleichzeitig,
       (SELECT count(*) FROM kanaele)            AS kanaele_gesamt
FROM snapshots;

\echo
\echo '=== 2. Lücken in der Messung (Abstand mehr als doppelt so lang wie üblich) ==='
WITH g AS (
  SELECT taken_at, taken_at - lag(taken_at) OVER (ORDER BY taken_at) AS abstand
  FROM snapshots WHERE complete
), m AS (
  SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM abstand)) AS median_s
  FROM g WHERE abstand IS NOT NULL
)
SELECT to_char(taken_at, 'YYYY-MM-DD HH24:MI') AS weiter_ab, abstand
FROM g, m
WHERE extract(epoch FROM abstand) > 2 * m.median_s
ORDER BY taken_at;

\echo
\echo '=== 3. Verteilung nach durchschnittlichen Zuschauern ==='
\echo '    gaming = überwiegend Spielkategorien; regelmäßig = zusätzlich mindestens 3 Streams im Zeitraum'
SELECT CASE stufe
         WHEN 1 THEN 'unter 1'          WHEN 2 THEN '1 bis unter 3'
         WHEN 3 THEN '3 bis unter 5'    WHEN 4 THEN '5 bis unter 10'
         WHEN 5 THEN '10 bis unter 25'  WHEN 6 THEN '25 bis unter 50'
         WHEN 7 THEN '50 bis unter 100' ELSE '100 und mehr'
       END                                                         AS zuschauer,
       count(*)                                                    AS alle,
       round(100.0 * count(*) / sum(count(*)) OVER (), 1)          AS alle_prozent,
       count(*) FILTER (WHERE gaming)                              AS gaming,
       count(*) FILTER (WHERE gaming_regelmaessig)                 AS gaming_regelmaessig
FROM stufen
GROUP BY stufe
ORDER BY stufe;

\echo
\echo '=== 4. Kanäle mit mindestens X durchschnittlichen Zuschauern ==='
SELECT schwelle AS mindestens,
       count(*) FILTER (WHERE avg_zuschauer >= schwelle)                          AS alle,
       count(*) FILTER (WHERE avg_zuschauer >= schwelle AND gaming)               AS gaming,
       count(*) FILTER (WHERE avg_zuschauer >= schwelle AND gaming_regelmaessig)  AS gaming_regelmaessig
FROM stufen, (VALUES (3), (10), (50), (100)) AS s(schwelle)
GROUP BY schwelle
ORDER BY schwelle;

\echo
\echo '=== 5. Zielgruppe: Gaming-Kanäle mit 3 bis unter 50 Zuschauern, häufigste Hauptkategorien ==='
SELECT hauptkategorie, count(*) AS kanaele, round(avg(avg_zuschauer), 1) AS avg_zuschauer
FROM stufen
WHERE gaming_regelmaessig AND avg_zuschauer >= 3 AND avg_zuschauer < 50
GROUP BY hauptkategorie
ORDER BY kanaele DESC
LIMIT 25;

\echo
\echo '=== 6. Punkte pro Stream nach Pool-Regel (Ø-Zuschauer × Stunden × 30, nur Spiele, ab 1 Stunde) ==='
WITH s AS (
  SELECT stream_id,
         avg(viewer_count)               AS avg_zuschauer,
         count(*) * :intervall_min / 60.0 AS stunden
  FROM proben
  WHERE ist_spiel
  GROUP BY stream_id
  HAVING count(*) * :intervall_min >= 60
)
SELECT count(*)                                                                         AS streams,
       round(percentile_cont(0.25) WITHIN GROUP (ORDER BY avg_zuschauer * stunden * 30)) AS p25,
       round(percentile_cont(0.50) WITHIN GROUP (ORDER BY avg_zuschauer * stunden * 30)) AS median,
       round(percentile_cont(0.75) WITHIN GROUP (ORDER BY avg_zuschauer * stunden * 30)) AS p75,
       round(percentile_cont(0.90) WITHIN GROUP (ORDER BY avg_zuschauer * stunden * 30)) AS p90,
       round(avg(stunden), 1)                                                           AS avg_stunden
FROM s;

-- CSV-Export ohne Login-Namen (für Tabellen und Diagramme).
\copy (SELECT hauptkategorie, avg_zuschauer, streams, stunden_live, anteil_spiele FROM kanaele ORDER BY avg_zuschauer DESC) TO '/export/kanaele.csv' WITH (FORMAT csv, HEADER)
\echo
\echo 'Export geschrieben: export/kanaele.csv'
