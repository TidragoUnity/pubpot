# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TwitchWorker.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# --- Laufzeit (klein, ohne SDK) ---
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

# Nicht als root laufen.
USER app

# Gesund, solange der Worker in den letzten 15 Minuten eine Runde gespeichert hat.
HEALTHCHECK --interval=1m --timeout=5s --start-period=2m \
  CMD test -n "$(find /tmp/heartbeat -mmin -15 2>/dev/null)" || exit 1

ENTRYPOINT ["dotnet", "TwitchWorker.dll"]
