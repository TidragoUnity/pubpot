# PubPot

Landingpage mit Anmeldung für Entwickler und Creator, dazu Twitch-Worker, Docker, CI/CD und Monitoring.

> Im Aufbau. Plan und Status: [docs/ROADMAP.md](docs/ROADMAP.md)

## Aufbau

| Ordner | Inhalt |
|---|---|
| `src/TwitchWorker` | Sammelt regelmäßig Stream-Daten von Twitch in PostgreSQL (.NET 10) |
| `tests/PubPot.Web.Tests` | Tests (xUnit v3) |
| `docs/` | Roadmap, Entscheidungen, Rückfragen, Journal, Checkliste |

## Bauen und testen

Voraussetzung: .NET SDK 11 (genaue Version in `global.json`). Die Projekte zielen auf .NET 10 (LTS).

```bash
dotnet build PubPot.slnx
dotnet test --solution PubPot.slnx
dotnet format PubPot.slnx --verify-no-changes
```
