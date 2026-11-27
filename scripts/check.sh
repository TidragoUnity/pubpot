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

[ -n "${in_group:-}" ] && echo "::endgroup::"
echo "==> Alles grün."
