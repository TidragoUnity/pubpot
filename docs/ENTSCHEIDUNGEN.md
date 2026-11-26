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
