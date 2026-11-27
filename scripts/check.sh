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
MSYS_NO_PATHCONV=1 docker run --rm -v "$monitoring_dir/prometheus:/etc/prometheus:ro" --entrypoint promtool \
  "$prometheus_image" check config /etc/prometheus/prometheus.yml

[ -n "${in_group:-}" ] && echo "::endgroup::"
echo "==> Alles grün."
