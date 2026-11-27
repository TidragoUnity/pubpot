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
    compose --profile worker logs --no-color || true
  fi
  compose --profile worker down -v --remove-orphans >/dev/null 2>&1 || true
  rm -f "$env_file"
}
trap cleanup EXIT

random() { od -An -N16 -tx1 /dev/urandom | tr -d ' \n'; }

cat > "$env_file" <<ENV
POSTGRES_PASSWORD=$(random)
PUBPOT_DB_PASSWORD=$(random)
TWITCH_DB_PASSWORD=$(random)
IMAGE_TAG=${IMAGE_TAG:-local}
EDGE_SUBNET=172.31.250.0/24
Smtp__Host=smtp.invalid
Smtp__FromAddress=smoke@example.test
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

echo "==> Starte Stack (IMAGE_TAG=${IMAGE_TAG:-local})"
if ! compose up -d --wait --wait-timeout 180 --no-build; then failed=1; exit 1; fi
compose --profile worker run --rm --no-deps migrate-twitch || failed=1

echo "==> Prüfe"
migrate_exit=$(docker inspect -f '{{.State.ExitCode}}' "$(compose ps -aq migrate)")
check "migrate mit Exit-Code 0 beendet" [ "$migrate_exit" = 0 ]
check "/health/ready über Caddy: 200" curl -fsk -o /dev/null https://localhost/health/ready
check "/de liefert die Startseite" sh -c 'curl -fsk https://localhost/de | grep -q "<title>.*PubPot"'
check "HTTP leitet auf HTTPS um (308)" sh -c '[ "$(curl -s -o /dev/null -w "%{http_code}" http://localhost/de)" = 308 ]'
check "kein Server-Header" sh -c '! curl -sk -D - -o /dev/null https://localhost/de | grep -qi "^server:"'
check "Web-Log: jede Zeile JSON mit UTC-Zeit" web_log_is_json

if [ "$failed" -ne 0 ]; then exit 1; fi
echo "==> Smoke-Test bestanden."
