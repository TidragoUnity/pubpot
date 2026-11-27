# Twitch-Worker. Build-Kontext ist die Repo-Wurzel (global.json, Directory.*.props liegen dort):
#   docker build -f src/TwitchWorker/Dockerfile -t pubpot-worker .
# Normalerweise baut Compose das Image: cd deploy && docker compose --profile worker build worker

# --- Build (SDK 11, siehe global.json und E-007) ---
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Erst nur die Dateien für "restore" kopieren: Diese Docker-Schicht bleibt im Cache,
# solange sich die Pakete nicht ändern. Das macht spätere Builds deutlich schneller.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TwitchWorker/TwitchWorker.csproj src/TwitchWorker/
RUN dotnet restore src/TwitchWorker/TwitchWorker.csproj

COPY src/TwitchWorker/ src/TwitchWorker/
RUN dotnet publish src/TwitchWorker/TwitchWorker.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# --- Laufzeit (E-036) ---
# "chiseled": ohne Shell, ohne Paketmanager, ohne root-Benutzer. Ohne "extra", weil der Worker weder
# Übersetzungen noch Zeitzonen braucht (alles in UTC).
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
# /metrics für Prometheus, nur im internen Docker-Netz (E-052). Der Worker hat keine Ports nach außen.
EXPOSE 9464

# Gesund, solange der Worker in den letzten 15 Minuten eine Runde gespeichert hat (Heartbeat-Datei in /tmp).
HEALTHCHECK --interval=1m --timeout=5s --start-period=2m \
  CMD ["dotnet", "TwitchWorker.dll", "healthcheck"]

ENTRYPOINT ["dotnet", "TwitchWorker.dll"]
