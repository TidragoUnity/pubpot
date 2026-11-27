#!/bin/sh
# Smoke-Test: startet den Stack aus deploy/compose.yaml so wie auf dem Server (ohne compose.override.yaml,
# fertige Images statt Build) und prüft, ob er wirklich läuft (E-042). Die CI ruft ihn nach dem Image-Build auf.
#
#   ./scripts/smoke-test.sh                 # mit den lokal gebauten Images (IMAGE_TAG=local)
#   IMAGE_TAG=<sha> ./scripts/smoke-test.sh # mit bestimmten Images, z. B. aus GHCR
#
# Läuft in einem eigenen Compose-Projekt mit Wegwerf-Passwörtern und eigenem Subnetz. Deine lokalen Daten
# bleiben unberührt. Belegt kurz die Ports 80 und 443 auf 127.0.0.1. Am Ende wird alles wieder entfernt.
set -eu
cd "$(dirname "$0")/../deploy"

project="pubpot-smoke"
env_file="$(mktemp)"
failed=0

compose() { docker compose -f compose.yaml -p "$project" --env-file "$env_file" "$@"; }

cleanup() {
  if [ "$failed" -ne 0 ]; then
    echo "==> Fehlgeschlagen. Logs aller Container:"
    compose --profile worker --profile monitoring logs --no-color || true
  fi
  # Backups gehören im Container root (E-056), deshalb über den Container löschen.
  compose run --rm --no-deps --entrypoint sh backup -c 'rm -rf /backups/daily /backups/weekly /backups/.status-*' \
    >/dev/null 2>&1 || true
  compose --profile worker --profile monitoring down -v --remove-orphans >/dev/null 2>&1 || true
  rmdir backups-smoke 2>/dev/null || true
  rm -f "$env_file"
}
trap cleanup EXIT

random() { od -An -N16 -tx1 /dev/urandom | tr -d ' \n'; }

cat > "$env_file" <<ENV
POSTGRES_PASSWORD=$(random)
PUBPOT_DB_PASSWORD=$(random)
TWITCH_DB_PASSWORD=$(random)
GRAFANA_ADMIN_PASSWORD=$(random)
IMAGE_TAG=${IMAGE_TAG:-local}
EDGE_SUBNET=172.31.250.0/24
Smtp__Host=smtp.invalid
Smtp__FromAddress=smoke@example.test
BACKUP_DIR=./backups-smoke
ENV

check() { # check <Beschreibung> <Befehl …>
  desc="$1"; shift
  if "$@"; then echo "  ok   $desc"; else echo "  FEHLER $desc"; failed=1; fi
}

# Jede Log-Zeile der Website ist ein JSON-Objekt, das mit der Zeit in UTC beginnt (E-050). Mindestens eine Zeile.
web_log_is_json() {
  log=$(compose logs --no-log-prefix --no-color web)
  [ -n "$log" ] && ! printf '%s\n' "$log" | grep -v '^{"Timestamp":"[0-9-]*T[0-9:.]*Z",.*}$' | grep -q .
}

# Caddy hängt im selben Netz wie web und bringt wget mit. Die App-Images haben bewusst kein Werkzeug dafür.
metrics_inside() {
  compose exec -T caddy wget -qO- http://web:9464/metrics | grep -q '^http_server_request_duration_seconds_count'
}

# Worker ohne Netz starten (kein Twitch, keine Datenbank) und /metrics aus seinem Netz-Namespace abfragen (E-052).
# wget kommt aus dem Caddy-Image, das Worker-Image hat bewusst keine Werkzeuge.
worker_metrics() {
  id=$(MSYS_NO_PATHCONV=1 docker run -d --network none --read-only --tmpfs /tmp --cap-drop ALL \
    --security-opt no-new-privileges:true -e Twitch__ClientId=smoke -e Twitch__ClientSecret=smoke \
    -e "ConnectionStrings__Default=Host=db.invalid;Database=x;Username=x;Password=x" \
    "ghcr.io/tidragounity/pubpot-worker:${IMAGE_TAG:-local}") || return 1
  wget_image=$(docker inspect -f '{{.Config.Image}}' "$(compose ps -q caddy)")
  result=1
  for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
    if docker run --rm --network "container:$id" --entrypoint wget "$wget_image" -qO- --header "Host: worker:9464" \
      http://127.0.0.1:9464/metrics 2>/dev/null | grep -q '^pubpot_worker_last_complete_round_time_seconds'; then
      result=0
      break
    fi
    sleep 1
  done
  docker rm -f "$id" >/dev/null
  return $result
}

# Monitoring (E-053): Prometheus erreicht alle Ziele. Der erste Abruf kommt nach bis zu 15 s, daher warten.
# Das erste Python, das wirklich läuft (unter Windows ist "python3" oft nur ein Platzhalter für den Store).
python=""
for candidate in python3 python; do
  if "$candidate" -c "" 2>/dev/null; then python=$candidate; break; fi
done
prom() { curl -sG --data-urlencode "query=$1" http://127.0.0.1:9090/api/v1/query; }
prometheus_targets_up() {
  for _ in $(seq 1 30); do
    prom 'count(up{job=~"prometheus|web|node|cadvisor|blackbox|alertmanager"} == 1)' | grep -q '"6"\]' && return 0
    sleep 2
  done
  prom 'up' >&2
  return 1
}
# Backups (E-056): beim Start sofort beide Datenbanken und die Rollen gesichert, Messwerte kommen in Prometheus an.
backup_files() {
  compose exec -T backup sh -c 'ls /backups/daily/pubpot-*.dump /backups/daily/twitch-*.dump /backups/daily/roles-*.sql' \
    >/dev/null
}
backup_metrics() {
  for _ in $(seq 1 30); do
    prom 'count(pubpot_backup_last_run_success == 1)' | grep -q '"2"' && return 0
    sleep 2
  done
  prom 'pubpot_backup_last_run_success' >&2
  return 1
}
# Alarme (E-055): Regeln geladen, Alertmanager verbunden, und im gesunden Stack feuert nichts.
alerting_ready() {
  curl -s http://127.0.0.1:9090/api/v1/rules | grep -q '"name":"WebsiteNichtErreichbar"' &&
    prom 'prometheus_notifications_alertmanagers_discovered' | grep -q '"1"\]' &&
    prom 'ALERTS{alertstate="firing"}' | grep -q '"result":\[\]'
}
# Blackbox (E-054): /health/ready und Startseite per HTTPS, Umleitung von http auf https. Alle drei bestanden.
website_probes() {
  for _ in $(seq 1 30); do
    prom 'count(probe_success{job=~"website|website_redirect"} == 1)' | grep -q '"3"\]' && return 0
    sleep 2
  done
  prom 'probe_success' >&2
  return 1
}
grafana() { curl -sf -u "admin:$(sed -n 's/^GRAFANA_ADMIN_PASSWORD=//p' "$env_file")" "http://127.0.0.1:3000$1"; }
grafana_ready() {
  grafana /api/datasources/uid/prometheus/health | grep -q '"status":"OK"' &&
    grafana /api/dashboards/uid/pubpot-start | grep -q '"title":"PubPot – Start"'
}
dashboard_queries() {
  "$python" ../scripts/check-dashboard-queries.py monitoring/grafana/dashboards/pubpot.json http://127.0.0.1:9090 \
    "Website von außen" "App (intern)" "Zertifikat gültig noch" "Anfragen pro Sekunde" "Anfragen nach Statuscode" "CPU pro Container" "Arbeitsspeicher pro Container" \
    "Festplatte belegt" "Server: CPU und Arbeitsspeicher"
}

# --- Restore-Test (E-057) -----------------------------------------------------------------------------------
db_psql() { compose exec -T db psql -U postgres -v ON_ERROR_STOP=1 -At "$@"; }

# Anmeldung wie ein Browser über Caddy: Formular laden (Token, Cookie, Zeitstempel), Mindestzeit abwarten, absenden.
signup_via_website() {
  jar=$(mktemp)
  page=$(curl -sk -c "$jar" https://localhost/de/signup/creator)
  token=$(printf '%s' "$page" | sed -n 's/.*name="__RequestVerificationToken"[^>]*value="\([^"]*\)".*/\1/p' | head -n 1)
  started=$(printf '%s' "$page" | sed -n 's/.*name="FormStarted" value="\([^"]*\)".*/\1/p' | head -n 1)
  sleep 4 # Spam:MinFillSeconds ist 3 Sekunden
  answer=$(curl -sk -b "$jar" -o /dev/null -w '%{http_code} %{redirect_url}' \
    --data-urlencode "__RequestVerificationToken=$token" --data-urlencode "FormStarted=$started" \
    --data-urlencode "Input.Name=Smoke Test" --data-urlencode "Input.Email=smoke-restore@example.test" \
    --data-urlencode "Input.Platforms=Twitch" --data-urlencode "Input.Channel=smoke_kanal" \
    --data-urlencode "Input.AudienceSize=Under10" --data-urlencode "Input.Genres=Roguelikes" \
    --data-urlencode "Input.Adult=true" --data-urlencode "Input.Consent=true" \
    https://localhost/de/signup/creator)
  rm -f "$jar"
  case "$answer" in
    "302 "*/de/signup/thanks*) [ "$(db_psql -d pubpot -c 'select count(*) from signups')" -ge 1 ] ;;
    *) echo "Antwort: $answer" >&2; return 1 ;;
  esac
}

# Pro Tabelle: Name, Anzahl Zeilen und Prüfsumme über den ganzen Inhalt.
fingerprint() {
  for table in $(db_psql -d "$1" -c "select quote_ident(schemaname) || '.' || quote_ident(tablename) from pg_tables
      where schemaname not in ('pg_catalog', 'information_schema') order by 1"); do
    echo "$table $(db_psql -d "$1" -c "select count(*) || ' ' || md5(coalesce(string_agg(t::text, '|' order by t::text), ''))
      from $table t")"
  done
}

# Neuestes Backup einer Datenbank in eine neue Datenbank <db>_restore_check einspielen und Tabelle für Tabelle vergleichen.
restore_matches() {
  file=$(compose exec -T backup sh -c "cd /backups && ls -1t daily/$1-*.dump | head -n 1")
  COMPOSE_ARGS="-f compose.yaml -p $project --env-file $env_file" ../scripts/restore.sh "$file" "$1_restore_check" --yes \
    >/dev/null || return 1
  original=$(fingerprint "$1")
  restored=$(fingerprint "$1_restore_check")
  if [ -n "$original" ] && [ "$original" = "$restored" ]; then
    printf '%s\n' "$original" | awk '{ rows += $2 } END { printf "       (%d Tabellen, %d Zeilen verglichen)\n", NR, rows }'
    return 0
  fi
  printf 'Original:\n%s\nWiederhergestellt:\n%s\n' "$original" "$restored" >&2
  return 1
}

website_ready() {
  for _ in $(seq 1 60); do
    curl -fsk -o /dev/null https://localhost/health/ready && return 0
    sleep 1
  done
  return 1
}

# Echte Wiederherstellung von pubpot mit scripts/restore.sh: stoppt web, spielt ein, startet web wieder.
restore_in_place() {
  file=$(compose exec -T backup sh -c "cd /backups && ls -1t daily/pubpot-*.dump | head -n 1")
  COMPOSE_ARGS="-f compose.yaml -p $project --env-file $env_file" ../scripts/restore.sh "$file" pubpot --yes >/dev/null &&
    website_ready &&
    [ "$(db_psql -d pubpot -c "select count(*) from signups where email = 'smoke-restore@example.test'")" = 1 ]
}

echo "==> Starte Stack (IMAGE_TAG=${IMAGE_TAG:-local})"
if ! compose --profile monitoring up -d --wait --wait-timeout 180 --no-build; then failed=1; exit 1; fi
compose --profile worker run --rm --no-deps migrate-twitch || failed=1

echo "==> Prüfe"
migrate_exit=$(docker inspect -f '{{.State.ExitCode}}' "$(compose ps -aq migrate)")
check "migrate mit Exit-Code 0 beendet" [ "$migrate_exit" = 0 ]
check "/health/ready über Caddy: 200" curl -fsk -o /dev/null https://localhost/health/ready
check "/de liefert die Startseite" sh -c 'curl -fsk https://localhost/de | grep -q "<title>.*PubPot"'
check "HTTP leitet auf HTTPS um (308)" sh -c '[ "$(curl -s -o /dev/null -w "%{http_code}" http://localhost/de)" = 308 ]'
check "kein Server-Header" sh -c '! curl -sk -D - -o /dev/null https://localhost/de | grep -qi "^server:"'
check "Web-Log: jede Zeile JSON mit UTC-Zeit" web_log_is_json
# Metriken (E-051): von außen nicht erreichbar, auch nicht mit gefälschtem Host-Header, im Docker-Netz schon.
check "/metrics von außen nicht erreichbar" sh -c '! curl -sk https://localhost/metrics | grep -q "# TYPE"'
check "/metrics auch nicht mit Host-Header :9464" sh -c '! curl -sk -H "Host: localhost:9464" https://localhost/metrics | grep -q "# TYPE"'
check "/metrics im Docker-Netz (web:9464) mit Anfragen-Metrik" metrics_inside
check "Worker: /metrics im Container (ohne Netz, ohne Twitch)" worker_metrics
check "Prometheus erreicht web, node-exporter, cAdvisor, Blackbox, Alertmanager und sich selbst" prometheus_targets_up
check "Alarmregeln geladen, Alertmanager verbunden, kein Alarm aktiv" alerting_ready
check "Backup beim Start: pubpot, twitch und Rollen gesichert" backup_files
check "Backup-Messwerte in Prometheus: beide erfolgreich" backup_metrics
check "Blackbox: Website per HTTPS über Caddy, http leitet um" website_probes
check "Grafana: Datenquelle OK, Start-Dashboard vorhanden" grafana_ready
check "Dashboard: alle Abfragen gültig, wichtige mit Daten" dashboard_queries

# Restore-Test (E-057) mit echten Daten. Beim Sichern und Vergleichen ist web gestoppt, damit sich nichts ändert
# (der Mail-Ausgang arbeitet sonst laufend weiter, weil smtp.invalid nie erreichbar ist).
echo "==> Restore-Test"
twitch_test_data() {
  db_psql -q -d twitch -c "insert into snapshots (taken_at, stream_count, viewer_sum, pages, duration_ms, complete, filter)
    values (now(), 1, 42, 1, 100, true, 'smoke')" >/dev/null
}
backup_now() { compose exec -T backup sh backup.sh now >/dev/null; }
check "Anmeldung über die Website (HTTPS, Formular-Token, Mindestzeit)" signup_via_website
check "Testdaten im Worker-Schema" twitch_test_data
compose stop web >/dev/null 2>&1
check "Backup von Hand (backup.sh now)" backup_now
check "Restore pubpot in neue Datenbank: alle Tabellen gleich" restore_matches pubpot
check "Restore twitch in neue Datenbank: alle Tabellen gleich" restore_matches twitch
compose start web >/dev/null 2>&1
check "Website nach Neustart wieder bereit" website_ready
check "restore.sh: pubpot zurückgespielt, web neu gestartet, Anmeldung da" restore_in_place

if [ "$failed" -ne 0 ]; then exit 1; fi
echo "==> Smoke-Test bestanden."
