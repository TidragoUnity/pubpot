#!/bin/sh
# Spielt die SQL-Skripte des Twitch-Workers ein (E-036). Läuft als einmaliger Container "migrate-twitch"
# im Postgres-Image, das psql schon mitbringt. Verbindung über die Standard-Variablen von psql:
#   PGHOST, PGDATABASE, PGUSER, PGPASSWORD
#
# Jedes Skript NNN_name.sql läuft genau einmal, in einer Transaktion, in der Reihenfolge der Nummern.
# Was gelaufen ist, steht in der Tabelle schema_migrations. Ein fehlerhaftes Skript bricht ab (Exit-Code 1),
# dann startet der Worker nicht.
#
# Neues Skript: nächste freie Nummer nehmen, z. B. 002_add_column.sql. Ein eingespieltes Skript nie mehr ändern.
set -eu

dir="${MIGRATIONS_DIR:-/migrations}"
# Hinweise wie "relation already exists, skipping" (bei IF NOT EXISTS) nicht ausgeben, nur Warnungen und Fehler.
export PGOPTIONS="${PGOPTIONS:-} -c client_min_messages=warning"

psql -v ON_ERROR_STOP=1 -q -c "CREATE TABLE IF NOT EXISTS schema_migrations (
    version    text        PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);"

applied=0
for file in "$dir"/[0-9][0-9][0-9]_*.sql; do
    [ -e "$file" ] || continue
    version=$(basename "$file" .sql)

    done_already=$(psql -v ON_ERROR_STOP=1 -tA -v version="$version" <<'SQL'
SELECT count(*) FROM schema_migrations WHERE version = :'version';
SQL
)
    [ "$done_already" = "0" ] || continue

    echo "Spiele $version ein ..."
    # Skript und Eintrag in schema_migrations in EINER Transaktion: Entweder beides oder nichts.
    { echo "BEGIN;"; cat "$file"; echo; echo "INSERT INTO schema_migrations (version) VALUES (:'version');"; echo "COMMIT;"; } \
        | psql -v ON_ERROR_STOP=1 -q -v version="$version"
    applied=$((applied + 1))
done

echo "Twitch-Schema aktuell ($applied neu eingespielt)."
