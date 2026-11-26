# Build-Kontext ist der Repo-Wurzelordner, weil global.json und Directory.*.props dort liegen:
#   docker build -f src/TwitchWorker/Dockerfile .
# (Wird in Phase 5 überarbeitet: Healthcheck, Metriken, gemeinsame Compose-Datei.)

# --- Build (SDK 11, siehe global.json) ---
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Erst nur die Dateien für "restore" kopieren: Diese Docker-Schicht bleibt im Cache,
# solange sich die Pakete nicht ändern. Das macht spätere Builds deutlich schneller.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TwitchWorker/TwitchWorker.csproj src/TwitchWorker/
RUN dotnet restore src/TwitchWorker/TwitchWorker.csproj

COPY src/TwitchWorker/ src/TwitchWorker/
RUN dotnet publish src/TwitchWorker/TwitchWorker.csproj -c Release -o /app --no-restore

# --- Laufzeit (.NET 10, klein, ohne SDK) ---
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Nicht als root laufen.
USER app

# Gesund, solange der Worker in den letzten 15 Minuten eine Runde gespeichert hat.
HEALTHCHECK --interval=1m --timeout=5s --start-period=2m \
  CMD test -n "$(find /tmp/heartbeat -mmin -15 2>/dev/null)" || exit 1

ENTRYPOINT ["dotnet", "TwitchWorker.dll"]
