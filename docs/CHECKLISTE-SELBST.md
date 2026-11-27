# Checkliste: Das musst du selbst erledigen

Dinge, die ich nicht für dich tun kann oder soll, weil sie Konten, Geld, Verträge oder Rechtsfragen betreffen.
Ich ergänze die Liste, sobald im Projekt etwas Neues dazukommt.

## Sofort
- [ ] Optional: Caddys lokalem Zertifikat vertrauen, damit `https://localhost` ohne Warnung lädt (README, „Caddy-Zertifikat vertrauen“). Vorerst klickst du die Warnung weg (F-017)
- [ ] Twitch Client Secret neu erzeugen, falls die alte `.env.example` irgendwo geteilt wurde (F-002)
- [x] GitHub CLI installiert, Anmeldung mit Fine-grained-Token nur für `pubpot`
- [ ] Token-Ablauf: ca. **24.02.2027** bei 90 Tagen. Das ist mitten im Next Fest, deshalb **vorher** erneuern (GitHub → Settings → Developer settings → Fine-grained tokens → Regenerate)
- [ ] GitHub → Settings → Emails: „Keep my email addresses private“ und „Block command line pushes that expose my email“ aktivieren

## Für Phase 8 (Twitch-Login)
- [ ] **Vor Schritt 8.1:** Eine neue Twitch-App nur für den Login anlegen, **nicht** die App deiner Studie verwenden (F-001). https://dev.twitch.tv/console/apps → „Register Your Application“:
  - Name **„PubPot Dev“** (Twitch-weit eindeutig, darf „Twitch“ nicht enthalten; falls vergeben: „PubPot Games Dev“). Für Production kommt später eine zweite App „PubPot“ (F-027)
  - OAuth Redirect URLs (Twitch nimmt nur `https`): `https://localhost/signin-twitch` (Docker über Caddy) und `https://localhost:5081/signin-twitch` (`dotnet run` mit dem Entwicklungszertifikat von .NET, kommt in 8.1)
  - Das Client-Secret zeigt Twitch nur einmal. Später gibt es nur „New Secret“, und das alte wird damit ungültig
  - Kategorie „Website Integration“, Client-Typ „Confidential“
  - Client-ID und Client-Secret in `deploy/.env` als `TwitchLogin__ClientId` und `TwitchLogin__ClientSecret` eintragen (für `dotnet run` per `dotnet user-secrets`, Anleitung folgt in 8.1). Nie ins Repo

## Bis Phase 6 (CI) bzw. 9 (CD)
- [x] GitHub-Repo angelegt (öffentlich) und gepusht: https://github.com/TidragoUnity/pubpot
- [ ] **Vor Phase 6:** Token-Rechte erweitern. GitHub → Settings → Developer settings → Fine-grained tokens → dein Token für `pubpot` → Edit → Repository permissions: **Workflows** „Read and write“, **Pull requests** „Read and write“, **Administration** „Read and write“ (nur für die Regeln auf `main` in 6.7). Speichern genügt, das Token selbst bleibt gleich
- [x] Images in GHCR öffentlich: GitHub hat das beim ersten Push automatisch so angelegt, nichts zu tun (F-018, E-043)
- [ ] GitHub Environments `staging` und `production` anlegen
- [ ] GitHub-Secrets eintragen (genaue Liste folgt in Phase 9)
- [ ] Branch-Schutz für `main`: PR mit grüner CI erforderlich

## Bis Phase 9 (Server) – vorher F-022 bis F-025 klären
- [ ] Hetzner VPS anlegen (F-008), SSH-Schlüssel hinterlegen
- [ ] Lokal ausprobieren (optional): `GRAFANA_ADMIN_PASSWORD` in `deploy/.env` setzen, `docker compose --profile monitoring up -d`, Grafana unter http://localhost:3000 (README, „Monitoring, Alarme und Backups“)
- [ ] Auf dem Server in `deploy/.env`: `GRAFANA_ADMIN_PASSWORD` (lang, zufällig) und `ALERT_EMAIL` (wohin Alarme gehen), `COMPOSE_PROFILES=worker,monitoring`
- [ ] Datenschutzerklärung, Abschnitt Backups, anwaltlich prüfen lassen: Wie werden Löschanfragen seit dem letzten Backup nach einer Wiederherstellung erneut umgesetzt? (TODO in der Datenschutzerklärung, E-057)
- [ ] VPS nach der Härtungsanleitung absichern (kommt in Phase 9)
- [ ] Domain `pubpot.games` registrieren
- [ ] DNS: A/AAAA-Einträge für `pubpot.games`, `www` und `staging` auf den VPS
- [ ] `deploy/.env` auf dem Server: `SITE_DOMAIN`, `BIND_ADDRESS=0.0.0.0`, SMTP-Zugang, Admin-Hash (`docker compose run --rm --no-deps web hash-password`), bei Bedarf `COMPOSE_PROFILES=worker` mit Twitch-Zugangsdaten. Lange Zufallspasswörter, nur Buchstaben und Ziffern
- [ ] Firewall: Ports 80 und 443 (TCP) sowie 443/UDP für HTTP/3 öffnen, sonst nichts außer SSH (Anleitung kommt in Phase 9)

## Bis zum Go-live
- [ ] SMTP-Anbieter in der EU wählen, Absenderdomain mit SPF, DKIM und DMARC einrichten
- [ ] Impressum und Datenschutzerklärung erstellen und **anwaltlich prüfen lassen**
- [ ] Einwilligungstext für die Formulare prüfen lassen (`Consent.*` in `SharedResource.resx`; bei Änderung `ConsentText.Version` erhöhen)
- [x] Aufbewahrungsfrist für bestätigte Anmeldungen festgelegt: 12 Monate Inaktivität, Warnmail, Löschung nach 30 Tagen (F-013, E-023)
- [ ] Diese Frist, die Warnmail (Texte `Mail.Retention.*` und `Mail.Kept.*` in `SharedResource.resx`) und den Abschnitt „Aufbewahrung“ der Datenschutzerklärung anwaltlich prüfen lassen
- [ ] Datenschutzerklärung: Notiz-Feld und Pipeline-Status im Admin-Bereich erwähnen (interne Akquise-Daten) und anwaltlich prüfen lassen
- [ ] Backup-Ziel wählen und buchen (F-008, noch offen) und einen Restore-Test selbst einmal durchführen
