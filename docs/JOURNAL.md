# Journal

Chronologisches Protokoll: was gemacht wurde, was als Nächstes kommt. Neueste Einträge oben.
Damit kann jede neue Arbeitssitzung nahtlos weitermachen.

---

## 2026-11-26 · Schritt 1.4 – Testprojekt, Phase 1 abgeschlossen
- `tests/PubPot.Web.Tests` mit xUnit v3 und einem Platzhalter-Test. `dotnet test --solution PubPot.slnx` ist grün.
- Stolperstein: Die Vorlage nutzt noch xUnit v2/VSTest. Mit xUnit v3 verlangt SDK 10+ die Microsoft Testing Platform, die jetzt in `global.json` eingeschaltet ist (E-010).
- **Als Nächstes:** Phase 2, Schritt 2.1 (Web-Projekt, Layout, Design-Grundlage).

## 2026-11-26 · Schritt 1.3 – Worker auf .NET 10
- Ziel `net10.0`, Npgsql 10.0.4, Microsoft.Extensions.Hosting 10.0.15. Der Code brauchte keine Änderungen für die neuen Versionen.
- Dockerfile baut jetzt aus dem Repo-Wurzelordner (wegen `global.json` und `Directory.*.props`), Build-Image `sdk:11.0`, Laufzeit-Image `runtime:10.0`. Neue `.dockerignore` im Wurzelordner.
- Test: Image gebaut und gegen eine temporäre Postgres-16-Instanz laufen lassen, mit Fake-Zugangsdaten, damit die laufende Studie nicht beeinträchtigt wird. Ergebnis: Schema angelegt, Runden gespeichert (`complete = false`, wie erwartet), Prozess läuft als Nutzer `app`.
- Gefunden und behoben: Npgsql ab Version 9 versucht, `libgssapi_krb5` zu laden, und schreibt einen irreführenden Fehler ins Log. GSS-Verschlüsselung ist jetzt im Code abgeschaltet, weil wir kein Kerberos nutzen.
- Offen für später: Ein echter Lauf mit Twitch-Zugangsdaten, sinnvoll nach dem Ende der Studie.

## 2026-11-26 · Schritt 1.2 – Grundgerüst
- `PubPot.slnx` (neues XML-Solution-Format), `global.json` auf SDK 11.0.100, zentrale Paketverwaltung, `.editorconfig`, `.gitattributes` (E-009).
- Der Worker nutzt die zentralen Versionen (Npgsql steht fest auf 8.0.9 statt `8.0.*`, weil Wildcards bei zentraler Verwaltung nicht erlaubt sind).
- `dotnet build` ohne Warnungen, `dotnet format --verify-no-changes` sauber.

## 2026-11-26 · Schritt 1.1 – Worker übernommen
- Quellcode aus `C:\Timo\Docker\twitch-worker` nach `src/TwitchWorker` kopiert. Das Original und der laufende Container bleiben unberührt.
- Vor dem Commit geprüft: Die Werte aus der alten `.env.example` stehen in keiner Datei im Repo. Die neue `.env.example` enthält nur Platzhalter.
- Build mit SDK 11 (Ziel noch `net8.0`) läuft grün.

## 2026-11-26 · F-004 und F-011 beantwortet
- F-004: Variante A, also Ziel `net10.0`, gebaut mit SDK 11 (E-007).
- F-011: Anonyme Commit-Adresse, `AUFTRAG.md` nur lokal. Die Historie wurde vor dem ersten Push umgeschrieben (E-008).
- Plan ist freigegeben → Phase 1 beginnt.

## 2026-11-26 · Antworten F-001 bis F-005 eingearbeitet
- F-001: Worker kommt als eigenes Projekt ins Repo. F-002: `.env` bleibt draußen. F-003: öffentliches Repo unter deinem Konto. F-005 ist damit erledigt (echte Freigabe möglich).
- F-004 (SDK 11 statt 10): Optionen erklärt, Entscheidung steht aus. Die .NET-10-Laufzeit ist installiert, es fehlt nur das SDK.
- Neu: F-011 (GitHub-Zugang, was ein öffentliches Repo bedeutet).
- **Als Nächstes:** Antwort zu F-004, dann Schritt 1.1.

## 2026-11-26 · Phase 0
**Erledigt**
- Auftrag gelesen (Kopie in [AUFTRAG.md](AUFTRAG.md)), bestehenden Worker angesehen.
- Git-Repo `C:\Timo\Docker\pubpot` angelegt, Plan-Dateien erstellt.

**Beobachtungen**
- Der Worker läuft gerade (Marktstudie, 168 h). Er bleibt unangetastet → F-001.
- `twitch-worker/.env.example` enthält vermutlich echte Zugangsdaten → F-002.
- Lokal ist kein .NET-10-SDK installiert (nur 9 und 11) → F-004.

**Als Nächstes**
- Warten auf dein OK zum Plan ([ROADMAP.md](ROADMAP.md)) und auf die Antworten zu F-001 bis F-004.
- Danach Schritt 1.1.
