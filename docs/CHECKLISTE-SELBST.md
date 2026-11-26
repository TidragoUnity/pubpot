# Checkliste: Das musst du selbst erledigen

Dinge, die ich nicht für dich tun kann oder soll, weil sie Konten, Geld, Verträge oder Rechtsfragen betreffen.
Ich ergänze die Liste, sobald im Projekt etwas Neues dazukommt.

## Sofort
- [ ] Twitch Client Secret neu erzeugen, falls die alte `.env.example` irgendwo geteilt wurde (F-002)
- [ ] GitHub CLI installieren (`winget install GitHub.cli`) und mit `! gh auth login` anmelden (F-011)

## Bis Phase 6/7 (CI/CD)
- [ ] GitHub-Repo anlegen (öffentlich) und pushen. Das übernehme ich, sobald `gh` angemeldet ist (F-003, F-011)
- [ ] GitHub Environments `staging` und `production` anlegen
- [ ] GitHub-Secrets eintragen (genaue Liste folgt in Phase 7)
- [ ] Branch-Schutz für `main`: PR mit grüner CI erforderlich

## Bis Phase 7 (Server)
- [ ] Hetzner VPS anlegen (F-008), SSH-Schlüssel hinterlegen
- [ ] VPS nach der Härtungsanleitung absichern (kommt in Phase 7)
- [ ] Domain `pubpot.games` registrieren
- [ ] DNS: A/AAAA-Einträge für `pubpot.games`, `www` und `staging` auf den VPS

## Bis zum Go-live
- [ ] SMTP-Anbieter in der EU wählen, Absenderdomain mit SPF, DKIM und DMARC einrichten
- [ ] Impressum und Datenschutzerklärung erstellen und **anwaltlich prüfen lassen**
- [ ] Einwilligungstext für die Formulare prüfen lassen
- [ ] Backup-Ziel buchen (F-008) und einen Restore-Test selbst einmal durchführen
