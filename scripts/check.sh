#!/bin/sh
# Prüft das Repo genauso wie die CI (.github/workflows/ci.yml). Bricht beim ersten Fehler ab.
#   ./scripts/check.sh
# Voraussetzung für die Datenbank-Tests: Docker läuft (Testcontainers startet ein eigenes Postgres).
set -e
cd "$(dirname "$0")/.."

# In GitHub Actions jeden Abschnitt im Log einklappbar machen.
section() {
  if [ "${GITHUB_ACTIONS:-}" = "true" ]; then
    [ -n "${in_group:-}" ] && echo "::endgroup::"
    echo "::group::$1"
    in_group=1
  else
    echo "==> $1"
  fi
}

section "Build (Warnungen als Fehler, wie in der CI)"
CI=true dotnet build PubPot.slnx -nologo -v q --no-incremental

section "Code-Stil"
dotnet format PubPot.slnx --verify-no-changes -v q

section "Tests"
dotnet test --solution PubPot.slnx --no-build

# Prometheus-Konfiguration mit promtool aus demselben Image, das Compose startet (E-053).
# "pwd -W" liefert unter Git Bash (Windows) einen Pfad, den Docker versteht.
section "Monitoring-Konfiguration (promtool)"
prometheus_image=$(sed -n 's|^ *image: \(prom/prometheus:.*\)$|\1|p' deploy/compose.yaml)
monitoring_dir="$(pwd -W 2>/dev/null || pwd)/deploy/monitoring"
# Einmal wie lokal (localhost) und einmal wie auf dem Server (echte Domain), über start.sh wie im Container.
for domain in localhost pubpot.example; do
  MSYS_NO_PATHCONV=1 docker run --rm -e SITE_DOMAIN="$domain" -v "$monitoring_dir/prometheus:/etc/prometheus:ro" \
    --entrypoint sh "$prometheus_image" /etc/prometheus/start.sh check
done

# Unit-Tests der Alarmregeln (E-055).
MSYS_NO_PATHCONV=1 docker run --rm -v "$monitoring_dir/prometheus:/etc/prometheus:ro" --entrypoint promtool \
  "$prometheus_image" test rules /etc/prometheus/tests/alerts_test.yml

# Alertmanager-Konfiguration: einmal ohne Mail-Empfänger, einmal mit SMTP-Anmeldung wie auf dem Server.
section "Alertmanager-Konfiguration (amtool)"
alertmanager_image=$(sed -n 's|^ *image: \(prom/alertmanager:.*\)$|\1|p' deploy/compose.yaml)
MSYS_NO_PATHCONV=1 docker run --rm -v "$monitoring_dir/alertmanager:/etc/alertmanager:ro" \
  --entrypoint sh "$alertmanager_image" /etc/alertmanager/start.sh check
MSYS_NO_PATHCONV=1 docker run --rm -v "$monitoring_dir/alertmanager:/etc/alertmanager:ro" \
  -e ALERT_EMAIL=ops@pubpot.example -e ALERT_SMTP_HOST=smtp.pubpot.example -e ALERT_SMTP_FROM=noreply@pubpot.example \
  -e ALERT_SMTP_USERNAME=noreply@pubpot.example -e ALERT_SMTP_PASSWORD=geheim \
  --entrypoint sh "$alertmanager_image" /etc/alertmanager/start.sh check

[ -n "${in_group:-}" ] && echo "::endgroup::"
echo "==> Alles grün."
