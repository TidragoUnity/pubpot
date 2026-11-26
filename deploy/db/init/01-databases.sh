#!/bin/sh
# Legt beim allerersten Start (leeres Volume) die Datenbanken und Benutzer an.
#
#   pubpot  – Datenbank der Web-App, Benutzer "pubpot"
#   twitch  – Datenbank des Workers, Benutzer "twitch"
#
# Jeder Benutzer besitzt nur seine eigene Datenbank und kann sich mit der anderen nicht verbinden
# (Prinzip der geringsten Rechte, siehe docs/ENTSCHEIDUNGEN.md E-011).
# Die Passwörter kommen aus Umgebungsvariablen und werden psql als Variablen übergeben,
# damit sie nicht im Skript stehen.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v pubpot_password="$PUBPOT_DB_PASSWORD" \
  -v twitch_password="$TWITCH_DB_PASSWORD" <<'SQL'
CREATE ROLE pubpot LOGIN PASSWORD :'pubpot_password';
CREATE DATABASE pubpot OWNER pubpot;
REVOKE ALL ON DATABASE pubpot FROM PUBLIC;

CREATE ROLE twitch LOGIN PASSWORD :'twitch_password';
CREATE DATABASE twitch OWNER twitch;
REVOKE ALL ON DATABASE twitch FROM PUBLIC;
SQL

echo "Datenbanken pubpot und twitch angelegt."
