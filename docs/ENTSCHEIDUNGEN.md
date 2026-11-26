# Entscheidungen

Kurzes Entscheidungslog (ADR-light). Jeder Eintrag beantwortet: **Was** wurde entschieden, **warum**, und **welche Alternative** gab es?
Das ist auch dein Lernmaterial: Hier steht, warum etwas so gebaut ist.

---

## E-001 · Fortschritt in Markdown-Dateien im Repo
**Entscheidung:** Status in `ROADMAP.md`, Verlauf in `JOURNAL.md`, Fragen in `RUECKFRAGEN.md`, Begründungen hier.
**Warum:** Alles liegt versioniert neben dem Code, jede Änderung ist in der Git-Historie sichtbar, und es braucht kein extra Werkzeug.
**Alternative:** GitHub Issues/Projects. Das ist sinnvoll, sobald das Repo auf GitHub liegt und mehrere Leute mitarbeiten.

## E-002 · Ein Commit pro Roadmap-Schritt, Nachricht beginnt mit der Schrittnummer
**Warum:** Kleine, lauffähige Commits lassen sich einzeln prüfen und zurückdrehen (`git revert`), und `git log` passt direkt zur Roadmap.

## E-003 · Repo getrennt vom laufenden Worker
**Entscheidung:** Neues Repo unter `C:\Timo\Docker\pubpot`, der Worker-Code wird hineinkopiert, nicht verschoben.
**Warum:** Docker Compose leitet den Projektnamen und damit den Volume-Namen (`twitch-worker_pgdata`) aus dem Ordnernamen ab. Ein Verschieben könnte die laufende Studie stören. Bestätigt durch F-001: Der Worker wird eigenes Projekt `src/TwitchWorker`.

## E-004 · Öffentliches GitHub-Repo
**Entscheidung (F-003):** Das Repo ist öffentlich, auch als Showcase.
**Folgen:** GitHub Actions ohne Minutenlimit, Environment-Freigaben (Required reviewers) und Secret Scanning sind kostenlos. Dafür gilt: Was gepusht ist, ist öffentlich und bleibt in der Historie. Secrets liegen nur in `.env` (per `.gitignore` ausgeschlossen) und in den GitHub-Secrets.

## E-005 · Production-Freigabe über GitHub Environment
**Entscheidung (F-005):** Production-Deploy bei Tag `v*`, mit manueller Freigabe im Environment `production`, wie im Auftrag. Möglich, weil das Repo öffentlich ist (E-004).
**Alternative (verworfen):** Nur ein per Hand gestarteter Workflow (`workflow_dispatch`). Er wäre bei einem privaten Repo ohne kostenpflichtigen GitHub-Plan nötig gewesen.

## E-006 · Keine Zugangsschlüssel im Chat
**Entscheidung:** Der Zugang zu GitHub läuft über `gh auth login`, das du selbst ausführst. Tokens werden nie in den Chat kopiert.
**Warum:** Was im Chat steht, landet im Verlauf. Ein Token, das dort liegt, gilt als kompromittiert.

## E-007 · Ziel `net10.0`, gebaut mit SDK 11
**Entscheidung (F-004, Variante A):** Alle Projekte zielen auf `net10.0` (LTS). Gebaut wird lokal, in der CI und im Docker-Build-Image mit SDK 11. Die Version legt `global.json` fest. Die Laufzeit-Images sind `aspnet:10.0` und `runtime:10.0`.
**Warum:** Das **SDK** ist das Werkzeug zum Bauen, die **Laufzeit** führt das Programm aus. Ein neueres SDK baut problemlos für eine ältere Zielversion. Wichtig ist nur, dass überall dasselbe SDK verwendet wird, sonst verhalten sich `dotnet format` und die Analyzer lokal anders als in der CI.
**Folge:** Dependabot darf Microsoft-, EF-Core- und Npgsql-Pakete nicht auf 11.x heben. Dafür kommt in Phase 6 eine Ignore-Regel.

## E-008 · Privatsphäre im öffentlichen Repo
**Entscheidung (F-011):** Die Commits laufen über die anonyme GitHub-Adresse, `docs/AUFTRAG.md` bleibt nur lokal (`.gitignore`).
**Lernpunkt:** Die Git-Historie umzuschreiben (`git filter-branch` bzw. `git filter-repo`) ist nur unbedenklich, **solange noch nichts gepusht ist**. Danach hätten andere Kopien die alte Historie, und Daten, die einmal veröffentlicht sind, muss man als öffentlich betrachten.
