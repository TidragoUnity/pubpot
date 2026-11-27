#!/bin/sh
# Backups der Datenbanken pubpot und twitch (Schritt 7.7, E-056). Läuft im Dienst "backup" (Postgres-Image).
#
# Aufruf im Container (Arbeitsordner /scripts), z. B. docker compose exec backup sh backup.sh now
#   sh backup.sh loop                     Dauerbetrieb: gleich beim Start, falls das letzte Backup älter als
#                                         24 Stunden ist, danach jede Nacht um BACKUP_TIME (Standard 03:00)
#   sh backup.sh now                      sofort ein Backup (z. B. vor einem Update)
#   sh backup.sh restore <Datei> <DB>     Datenbank aus einer Datei wiederherstellen (über scripts/restore.sh)
#
# Ablage unter /backups (auf dem Server: BACKUP_DIR):
#   daily/   jede Nacht, 7 Tage aufbewahrt          weekly/  sonntags eine Kopie, 4 Wochen aufbewahrt
# Pro Lauf: <db>-<Zeit>.dump je Datenbank (pg_dump, Custom-Format) und roles-<Zeit>.sql (Benutzer und Rechte).
# Gelöscht wird nur nach einem erfolgreichen Lauf. Schlagen Backups fehl, bleiben die alten also erhalten.
#
# Für Prometheus (über den node-exporter) entsteht /metrics/pubpot_backup.prom mit Erfolg, Zeitpunkt und Größe.
set -eu

databases="pubpot twitch"
dir=/backups
metrics=/metrics/pubpot_backup.prom
keep_daily_days=7
keep_weekly_days=28

log() { echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $*"; }

# Backups enthalten personenbezogene Daten: nur für den Eigentümer lesbar.
umask 077
mkdir -p "$dir/daily" "$dir/weekly" "$(dirname "$metrics")"

newest() { # newest <Muster in daily/>: neueste Datei oder nichts
  ls -1t "$dir"/daily/$1 2>/dev/null | head -n 1
}

# Messwerte aus dem Zustand auf der Platte, damit sie auch nach einem Neustart stimmen.
write_metrics() {
  tmp="$metrics.tmp"
  {
    echo "# HELP pubpot_backup_last_run_success 1, wenn der letzte Lauf für diese Datenbank ein gültiges Backup erzeugt hat."
    echo "# TYPE pubpot_backup_last_run_success gauge"
    for db in $databases; do
      status=$(cat "$dir/.status-$db" 2>/dev/null || echo ok)
      if [ "$status" = ok ]; then value=1; else value=0; fi
      echo "pubpot_backup_last_run_success{database=\"$db\"} $value"
    done
    echo "# HELP pubpot_backup_last_success_timestamp_seconds Zeitpunkt des neuesten gültigen Backups (Unix-Zeit)."
    echo "# TYPE pubpot_backup_last_success_timestamp_seconds gauge"
    for db in $databases; do
      file=$(newest "$db-*.dump")
      if [ -n "$file" ]; then
        echo "pubpot_backup_last_success_timestamp_seconds{database=\"$db\"} $(stat -c %Y "$file")"
      fi
    done
    echo "# HELP pubpot_backup_last_size_bytes Größe des neuesten gültigen Backups."
    echo "# TYPE pubpot_backup_last_size_bytes gauge"
    for db in $databases; do
      file=$(newest "$db-*.dump")
      if [ -n "$file" ]; then
        echo "pubpot_backup_last_size_bytes{database=\"$db\"} $(stat -c %s "$file")"
      fi
    done
  } > "$tmp"
  # Der node-exporter läuft unter einem anderen Benutzer und muss die Datei lesen können.
  chmod 644 "$tmp"
  mv "$tmp" "$metrics"
}

# Eine Datenbank sichern: erst in eine temporäre Datei, prüfen, dann umbenennen. Eine halbe Datei zählt nie.
backup_db() { # backup_db <db> <Zeitstempel>
  db=$1
  target="$dir/daily/$db-$2.dump"
  tmp="$target.tmp"
  start=$(date +%s)
  if pg_dump --format=custom --dbname="$db" --file="$tmp" && pg_restore --list "$tmp" > /dev/null && [ -s "$tmp" ]; then
    mv "$tmp" "$target"
    echo ok > "$dir/.status-$db"
    log "Backup $db: $(stat -c %s "$target") Bytes in $(( $(date +%s) - start )) s → daily/$(basename "$target")"
    return 0
  fi
  rm -f "$tmp"
  echo failed > "$dir/.status-$db"
  log "FEHLER: Backup $db fehlgeschlagen."
  return 1
}

run() {
  stamp=$(date +%Y-%m-%d_%H%M%S)
  failed=0
  for db in $databases; do
    backup_db "$db" "$stamp" || failed=1
  done

  # Benutzer und Rechte (ohne sie ließe sich ein Dump nicht mit den richtigen Eigentümern einspielen).
  if pg_dumpall --roles-only --file="$dir/daily/roles-$stamp.sql.tmp"; then
    mv "$dir/daily/roles-$stamp.sql.tmp" "$dir/daily/roles-$stamp.sql"
  else
    rm -f "$dir/daily/roles-$stamp.sql.tmp"
    log "FEHLER: Sicherung der Rollen fehlgeschlagen."
    failed=1
  fi

  if [ "$failed" -eq 0 ]; then
    # Sonntags eine Kopie für die wöchentliche Aufbewahrung.
    if [ "$(date +%u)" = 7 ]; then
      cp -p "$dir"/daily/*-"$stamp".* "$dir/weekly/"
    fi
    # Aufräumen nur nach Erfolg.
    find "$dir/daily" -type f -mmin +$((keep_daily_days * 1440)) -delete
    find "$dir/weekly" -type f -mmin +$((keep_weekly_days * 1440)) -delete
  fi

  write_metrics
  return "$failed"
}

seconds_until() { # seconds_until <HH:MM>: Sekunden bis zum nächsten Zeitpunkt
  now=$(date +%s)
  next=$(date -d "$1" +%s)
  if [ "$next" -le "$now" ]; then next=$(date -d "tomorrow $1" +%s); fi
  echo $((next - now))
}

restore() { # restore <Datei relativ zu /backups> <Ziel-Datenbank>
  file="$dir/$1"
  db=$2
  case "$db" in *[!a-z0-9_]* | "") log "Ungültiger Datenbankname: $db"; exit 2 ;; esac
  [ -f "$file" ] || { log "Datei nicht gefunden: $1"; exit 2; }
  pg_restore --list "$file" > /dev/null || { log "Keine gültige Backup-Datei: $1"; exit 2; }

  # Eigentümer wie beim Anlegen (deploy/db/init): pubpot → pubpot, twitch → twitch. Andere Namen (z. B. für
  # einen Test-Restore) bekommen den Eigentümer der Quelldatenbank aus dem Dateinamen.
  source_db=$(basename "$file" | sed 's/-[0-9].*$//')
  owner=$db
  case "$db" in pubpot | twitch) ;; *) owner=$source_db ;; esac

  log "Stelle $db aus $1 wieder her (Eigentümer $owner) ..."
  dropdb --if-exists --force "$db"
  createdb --owner="$owner" "$db"
  psql -q -v ON_ERROR_STOP=1 -d postgres -c "REVOKE ALL ON DATABASE \"$db\" FROM PUBLIC"
  pg_restore --exit-on-error --no-owner --role="$owner" --dbname="$db" "$file"
  log "Fertig: $db wiederhergestellt."
}

case "${1:-loop}" in
  now)
    run
    ;;
  restore)
    restore "${2:-}" "${3:-}"
    ;;
  loop)
    log "Backup-Dienst gestartet: täglich um ${BACKUP_TIME:-03:00} (${TZ:-UTC}), Ablage $dir."
    # Fehlt ein Backup der letzten 24 Stunden (z. B. nach dem ersten Start), gleich eines anlegen.
    if [ -z "$(find "$dir/daily" -name 'pubpot-*.dump' -mmin -1440 2>/dev/null)" ]; then
      run || true
    else
      write_metrics
    fi
    while true; do
      sleep "$(seconds_until "${BACKUP_TIME:-03:00}")"
      run || true
    done
    ;;
  *)
    echo "Aufruf: backup.sh loop | now | restore <Datei> <Datenbank>" >&2
    exit 2
    ;;
esac
