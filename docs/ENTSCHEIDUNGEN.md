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
**Warum:** Docker Compose leitet den Projektnamen und damit den Volume-Namen (`twitch-worker_pgdata`) aus dem Ordnernamen ab. Ein Verschieben könnte die laufende Studie stören. Vorbehaltlich F-001.
