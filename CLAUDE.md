# PubPot – Arbeitsanweisungen für Claude

Auftrag: `docs/AUFTRAG.md`. Sprache im Projekt: Deutsch (Code-Bezeichner Englisch).

## Zu Beginn jeder Sitzung
Nur `START-SESSION.md` lesen: Überblick, Stand und nächste Aufgabe. Weitere Dateien unter `docs/` nur öffnen, wenn die Aufgabe sie braucht. Das hält den Kontext klein.
Beantwortet der Nutzer eine Rückfrage, die Antwort in `docs/RUECKFRAGEN.md` übernehmen und die Entscheidung in `docs/ENTSCHEIDUNGEN.md` festhalten.

## Am Ende jeder Sitzung
In `START-SESSION.md` die Abschnitte „Stand“ und „Nächste Aufgabe“ aktualisieren.

## Arbeitsweise
- Ein Roadmap-Schritt = ein lauffähiger Commit. Commit-Nachricht: `<Schritt>: <Was>`, z. B. `1.3: Worker auf .NET 10 gehoben`.
- Mit jedem Commit auch `ROADMAP.md` (Status) und `JOURNAL.md` (Eintrag) aktualisieren.
- Neue Phase: zuerst in `ROADMAP.md` fein planen.
- Unklar, Abweichung vom Auftrag oder Kosten: Frage in `RUECKFRAGEN.md` stellen und warten.
- Wichtige Entscheidungen kurz erklären (der Nutzer lernt DevOps), Begründung in `ENTSCHEIDUNGEN.md`.
- Keine Secrets im Repo. Rechtstexte nur als Platzhalter mit TODO und „anwaltlich prüfen lassen“.
- Den laufenden Worker unter `C:\Timo\Docker\twitch-worker` nicht anfassen (siehe F-001).
