#!/bin/sh
# Stellt eine Datenbank aus einem Backup wieder her (Schritt 7.7, E-056).
#
#   ./scripts/restore.sh                                     zeigt die vorhandenen Backups
#   ./scripts/restore.sh daily/pubpot-2026-11-28_030000.dump  stellt "pubpot" daraus wieder her (fragt vorher)
#   ./scripts/restore.sh <Datei> <Datenbank> [--yes]         in eine andere Datenbank, z. B. pubpot_restore_test
#
# Die Datei liegt relativ zum Backup-Ordner (BACKUP_DIR, Standard deploy/backups). Die Ziel-Datenbank wird
# gelöscht und neu angelegt. Während der Wiederherstellung sind web und worker gestoppt, danach laufen sie wieder.
# Zusätzliche Compose-Optionen (z. B. -p oder --env-file) über COMPOSE_ARGS.
set -eu
cd "$(dirname "$0")/../deploy"

compose() { docker compose ${COMPOSE_ARGS:-} "$@"; }

if [ $# -eq 0 ]; then
  echo "Vorhandene Backups (neueste zuerst):"
  compose exec -T backup sh -c 'cd /backups && ls -1t daily/*.dump weekly/*.dump 2>/dev/null' || true
  echo
  echo "Aufruf: ./scripts/restore.sh <Datei> [Datenbank] [--yes]"
  exit 0
fi

file=$1
db=${2:-$(basename "$file" | sed 's/-[0-9].*$//')}
yes=${3:-}
[ "${2:-}" = "--yes" ] && { db=$(basename "$file" | sed 's/-[0-9].*$//'); yes=--yes; }

if [ "$yes" != "--yes" ]; then
  printf 'Datenbank "%s" wird GELÖSCHT und aus %s neu aufgebaut. Fortfahren? [ja/NEIN] ' "$db" "$file"
  read -r answer
  [ "$answer" = "ja" ] || { echo "Abgebrochen."; exit 1; }
fi

# Nur die Dienste anhalten, die diese Datenbank benutzen und gerade laufen.
case "$db" in
  pubpot) users=web ;;
  twitch) users=worker ;;
  *) users="" ;;
esac
running=""
for service in $users; do
  if [ -n "$(compose ps -q --status running "$service" 2>/dev/null)" ]; then
    running="$running $service"
  fi
done
if [ -n "$running" ]; then
  echo "Stoppe$running ..."
  compose stop $running
fi

status=0
compose exec -T backup sh backup.sh restore "$file" "$db" || status=$?

if [ -n "$running" ]; then
  echo "Starte$running wieder ..."
  compose start $running
fi
exit "$status"
