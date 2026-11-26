# Journal

Chronologisches Protokoll: was gemacht wurde, was als Nächstes kommt. Neueste Einträge oben.
Damit kann jede neue Arbeitssitzung nahtlos weitermachen.

---

## 2026-11-26 · Schritt 2.4 – Startseite
- Abschnitte: Hero mit zwei Zielgruppen-Karten, „So funktioniert's“ (3 Schritte), Rechenbeispiel (als „Beispiel – keine Prognose“ markiert), Für Entwickler, Für Creator, Pilot mit Next-Fest-Termin, FAQ (9 Fragen, `<details>` ohne JavaScript), Abschluss-CTA.
- Texte aus dem Auftrag abgeleitet, ohne Einnahmeversprechen („Rechne mit einem kleinen Zuverdienst, nicht mit einem Einkommen“). Bitte inhaltlich prüfen → F-012.
- **Lighthouse (Release-Build, `/de`):** mobil 99/100/100/100, Desktop 100/100/100/100 (Performance/Accessibility/Best Practices/SEO).
- Kontraste zusätzlich selbst berechnet, weil Lighthouse nur den hellen Modus prüft. Dabei gefunden: Der Akzentton im hellen Modus hatte in grauen Abschnitten 4,36:1 (< 4,5). Er ist jetzt dunkler (`#92400e`, 6,16:1).
- Stolperstein: Ein eigenes Ersetzungsskript (geschützte Leerzeichen vor % und €) hat durch einen Escaping-Fehler Steuerzeichen in die Texte geschrieben. Der Build hat die ungültige `.resx` sofort abgelehnt, die sieben Stellen sind repariert und geprüft.

## 2026-11-26 · Schritt 2.3 – Restliche Seiten
- Reihenfolge mit 2.4 getauscht, damit die Links der Startseite sofort funktionieren.
- Neue Seiten jeweils unter `/de` und `/en`: `signup/developer`, `signup/creator`, `signup/thanks`, `signup/confirm`, `legal/imprint`, `legal/privacy`. URLs werden klein geschrieben (`LowercaseUrls`).
- Impressum und Datenschutz sind Platzhalter mit TODO und „anwaltlich prüfen lassen“, dazu `noindex`. Die Datenschutzseite listet **technische Fakten** als Arbeitsgrundlage für den Anwalt, ausdrücklich als „kein Rechtstext“ gekennzeichnet. Diese Liste wächst mit dem Projekt mit.
- Danke- und Bestätigungsseite: `noindex`. Die Logik (Token prüfen) folgt in Phase 3.
- `robots.txt`: alles erlaubt außer `/lang/`.
- Geprüft: Alle 14 Seiten liefern 200, kein Ressourcen-Schlüssel erscheint als Rohtext, kein Konsolenfehler.

## 2026-11-26 · Schritt 2.2 – Deutsch und Englisch
- Sprachpräfix per Konvention, Weiterleitung von `/`, Umschalter mit Cookie, Sprachvorschlag, hreflang/canonical, lokalisierte Fehler- und 404-Seiten (E-017).
- Mit curl geprüft: Weiterleitung für de/en/fr/leer, Cookie hat Vorrang, `/fr` und `/DE` → 404, Open Redirect über `returnPath=//evil.com` blockiert.
- Im Browser geprüft: Vorschlagsleiste → Klick → `/en` ohne Leiste → zurück auf `/de` ohne Leiste.
- **Für die Datenschutzerklärung notiert:** Cookie `pubpot_lang` (nur nach Klick, 1 Jahr), `localStorage` `pubpot-theme` (nur im Browser, wird nicht übertragen).

## 2026-11-26 · Schritt 2.1 – Web-Projekt und Design-Grundlage
- `src/PubPot.Web` von Hand angelegt (ohne Bootstrap/jQuery aus der Vorlage, E-015). Layout mit Kopfzeile, Wortmarke „Pub**Pot**“, Navigation (ab 960 px sichtbar), Hell/Dunkel-Schalter und Fußzeile.
- Schriften Inter und Space Grotesk (Fontsource 5.3.0, Latin-Teilmenge mit Umlauten und €) selbst gehostet.
- Im Browser geprüft (Screenshots hell/dunkel/mobil): Schriften laden, keine Konsolenfehler, auf 390 px Breite kein seitlicher Scroll.
- Stolperstein: Beim ersten Test fehlten CSS und JS. Ursache war der Startbefehl (`dotnet run --environment`), siehe E-016.
- Texte sind noch fest auf Deutsch, die Lokalisierung folgt in 2.2.

## 2026-11-26 · Rückfragen F-006 bis F-010 beantwortet
- F-006: getrennte Datenbanken für Web und Worker (E-011). F-007: kein externer Check (E-012). F-010: nur E-Mail (E-014).
- F-008/F-009: Einen VPS gibt es noch nicht. Erst läuft alles lokal, Monitoring, Alarme und Backups eingeschlossen, danach kommt der VPS. Die Phasen 7 und 8 sind in der Roadmap getauscht (E-013).
- `RUECKFRAGEN.md` neu gegliedert in „Offen“ und „Beantwortet“. Dabei einen Fehler von mir behoben: Der ursprüngliche Text von F-004 war unter F-011 gerutscht.
- Zurzeit sind keine Fragen offen.
- **Als Nächstes:** Phase 2, sobald du OK gibst.

## 2026-11-26 · Erster Push nach GitHub
- `gh` mit Fine-grained-Token angemeldet. Der Token hat nur Zugriff auf `pubpot` (Least Privilege).
- Vor dem Push die gesamte Historie geprüft: keine Twitch-Zugangsdaten, kein altes DB-Passwort, keine echte E-Mail-Adresse, keine `.env`, keine `AUFTRAG.md`.
- Stolperstein: `git push` scheiterte mit `unable to get local issuer certificate`. Git nutzte OpenSSL mit eigener Zertifikatsliste, `gh` dagegen den Windows-Speicher. Lösung: `git config http.sslBackend schannel`, nur für dieses Repo. Die Zertifikate werden weiter geprüft, nur eben gegen den Windows-Speicher.
- Repo: https://github.com/TidragoUnity/pubpot

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
