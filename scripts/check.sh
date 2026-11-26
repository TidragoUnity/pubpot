#!/bin/sh
# Prüft das Repo so, wie es später die CI tut. Bricht beim ersten Fehler ab.
#   ./scripts/check.sh
# Voraussetzung für die Datenbank-Tests: Docker läuft (Testcontainers startet ein eigenes Postgres).
set -e
cd "$(dirname "$0")/.."

echo "==> Build (Warnungen als Fehler, wie in der CI)"
CI=true dotnet build PubPot.slnx -nologo -v q --no-incremental

echo "==> Code-Stil"
dotnet format PubPot.slnx --verify-no-changes -v q

echo "==> Tests"
dotnet test --solution PubPot.slnx --no-build

echo "==> Alles grün."
