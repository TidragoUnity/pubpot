#!/bin/sh
# Startet Alertmanager mit den SMTP-Einstellungen aus der Umgebung (E-055). Alertmanager liest selbst keine
# Umgebungsvariablen in seiner Konfiguration. Deshalb setzt dieses Skript die Werte in alertmanager.yml ein und
# schreibt das Ergebnis nach /tmp (das Dateisystem ist sonst schreibgeschützt).
#
#   sh start.sh [Alertmanager-Optionen …]   startet Alertmanager
#   sh start.sh check                        prüft nur die fertige Konfiguration (scripts/check.sh)
set -eu

# Nur Zeichen, die in Hostnamen und Mail-Adressen vorkommen. Alles andere könnte die Konfiguration verändern.
allowed() { # allowed <Name> <Wert> <Muster für verbotene Zeichen>
  case "$2" in
    *[$3]*)
      echo "$1 \"$2\" enthält unerlaubte Zeichen." >&2
      exit 1
      ;;
  esac
}

host="${ALERT_SMTP_HOST:-}"
port="${ALERT_SMTP_PORT:-587}"
from="${ALERT_SMTP_FROM:-}"
username="${ALERT_SMTP_USERNAME:-}"
to="${ALERT_EMAIL:-}"
allowed ALERT_SMTP_HOST "$host" '!A-Za-z0-9.-'
allowed ALERT_SMTP_PORT "$port" '!0-9'
allowed ALERT_SMTP_FROM "$from" '!A-Za-z0-9@._+-'
allowed ALERT_SMTP_USERNAME "$username" '!A-Za-z0-9@._+-'
allowed ALERT_EMAIL "$to" '!A-Za-z0-9@._+-'

# "None" (lokal, Mailpit) ohne Verschlüsselung, sonst STARTTLS bzw. bei Port 465 direkt TLS.
if [ "${ALERT_SMTP_SECURITY:-StartTls}" = "None" ]; then require_tls=false; else require_tls=true; fi

if [ -n "$to" ] && [ -n "$host" ] && [ -n "$from" ]; then
  receiver=mail
else
  receiver=aus
  # Platzhalter, damit die Konfiguration gültig bleibt. Der Empfänger "mail" wird dann nicht benutzt.
  host="${host:-localhost}" from="${from:-alertmanager@localhost}" to="${to:-niemand@localhost}"
  echo "Keine Alarm-Mails: ALERT_EMAIL (oder Notifications__Email), Smtp__Host und Smtp__FromAddress setzen." >&2
fi

umask 077
printf '%s' "${ALERT_SMTP_PASSWORD:-}" > /tmp/smtp_password
sed -e "s/__SMTP_HOST__/$host/" -e "s/__SMTP_PORT__/$port/" -e "s/__SMTP_FROM__/$from/" \
  -e "s/__SMTP_USERNAME__/$username/" -e "s/__SMTP_REQUIRE_TLS__/$require_tls/" \
  -e "s/__ALERT_EMAIL__/$to/" -e "s/__RECEIVER__/$receiver/" \
  /etc/alertmanager/alertmanager.yml > /tmp/alertmanager.yml

if [ "${1:-}" = "check" ]; then
  exec amtool check-config /tmp/alertmanager.yml
fi

exec /bin/alertmanager --config.file=/tmp/alertmanager.yml --storage.path=/alertmanager "$@"
