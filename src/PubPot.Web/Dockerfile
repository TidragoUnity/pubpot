# PubPot Web-App. Build-Kontext ist die Repo-Wurzel (global.json, Directory.*.props liegen dort):
#   docker build -f src/PubPot.Web/Dockerfile -t pubpot-web .
# Normalerweise baut Compose das Image: cd deploy && docker compose build web
# Migrations-Image (Stufe "migrate"):  docker build -f src/PubPot.Web/Dockerfile --target migrate -t pubpot-migrate .

# --- Build (SDK 11, siehe global.json und E-007) ---
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Erst nur die Dateien für "restore" kopieren: Diese Schicht bleibt im Cache, solange sich die Pakete nicht
# ändern. Eine geänderte .cshtml baut dann in Sekunden statt alle Pakete neu zu laden.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/PubPot.Web/PubPot.Web.csproj src/PubPot.Web/
RUN dotnet restore src/PubPot.Web/PubPot.Web.csproj

COPY src/PubPot.Web/ src/PubPot.Web/
# UseAppHost=false: keine zusätzliche Startdatei, gestartet wird mit "dotnet PubPot.Web.dll".
RUN dotnet publish src/PubPot.Web/PubPot.Web.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# --- Migrations-Bundle (E-035) ---
# Eine einzelne ausführbare Datei mit allen Migrationen. Sie läuft als einmaliger Container VOR der App
# (compose: Dienst "migrate") und braucht nur ConnectionStrings__Default, siehe Data/AppDbContextFactory.cs.
FROM build AS bundle
# dotnet ef lädt beim Erzeugen die App selbst, und die braucht die .NET-10-Laufzeit. Im SDK-11-Image steckt nur
# die Laufzeit 11. Deshalb holen wir die 10er aus dem offiziellen Laufzeit-Image (nur in dieser Build-Stufe).
COPY --from=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra /usr/share/dotnet/shared/ /usr/share/dotnet/shared/
COPY dotnet-tools.json ./
RUN dotnet tool restore
# Zum Erzeugen braucht EF einen DbContext, aber keine Verbindung: Platzhalter nur für diesen Befehl.
# Der echte Connection-String kommt erst beim Start des Containers (ConnectionStrings__Default).
RUN ConnectionStrings__Default="Host=nur-zum-bauen"     dotnet ef migrations bundle --project src/PubPot.Web/PubPot.Web.csproj --configuration Release       --output /bundle/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS migrate
WORKDIR /app
COPY --from=bundle /bundle/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# --- Laufzeit (E-034) ---
# Steht als letzte Stufe, damit "docker build" ohne --target die Web-App baut.
# "chiseled": Ubuntu ohne Shell, ohne Paketmanager, ohne root-Benutzer. "extra" bringt ICU und Zeitzonen mit,
# die wir für Deutsch/Englisch und Europe/Berlin brauchen.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS web
WORKDIR /app
COPY --from=build /app .

# Das Image läuft ohnehin als Benutzer "app" (UID 1654). Hier noch einmal ausdrücklich.
USER $APP_UID
EXPOSE 8080

# Kein curl im Image: Die App prüft sich selbst (HealthCheckCommand). Docker startet den Container
# nicht neu, aber Compose und "docker ps" sehen "unhealthy", und Caddy wartet auf "healthy".
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD ["dotnet", "PubPot.Web.dll", "healthcheck"]

ENTRYPOINT ["dotnet", "PubPot.Web.dll"]
