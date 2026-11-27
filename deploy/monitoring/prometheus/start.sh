#!/bin/sh
# Startet Prometheus mit der Domain aus SITE_DOMAIN (E-054). Prometheus selbst liest keine Umgebungsvariablen in
# seiner Konfiguration. Deshalb ersetzt dieses Skript __SITE_DOMAIN__ und __TLS__ in prometheus.yml und schreibt
# das Ergebnis nach /tmp (das Dateisystem ist sonst schreibgeschützt).
#
#   sh start.sh [Prometheus-Optionen …]   startet Prometheus
#   sh start.sh check                      prüft nur die fertige Konfiguration samt Regeln (scripts/check.sh)
set -eu

domain="${SITE_DOMAIN:-localhost}"
# Nur Zeichen, die in einem Hostnamen vorkommen. Alles andere könnte die Konfiguration verändern.
case "$domain" in
  *[!A-Za-z0-9.-]* | "")
    echo "SITE_DOMAIN \"$domain\" ist kein gültiger Hostname." >&2
    exit 1
    ;;
esac

# localhost: Zertifikat von Caddys eigener Zertifizierungsstelle, sonst ein öffentliches (Let's Encrypt).
if [ "$domain" = "localhost" ]; then tls=local; else tls=public; fi

sed -e "s/__SITE_DOMAIN__/$domain/g" -e "s/__TLS__/$tls/g" /etc/prometheus/prometheus.yml > /tmp/prometheus.yml

if [ "${1:-}" = "check" ]; then
  exec promtool check config /tmp/prometheus.yml
fi

exec /bin/prometheus --config.file=/tmp/prometheus.yml "$@"
