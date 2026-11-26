# Checkliste: Das musst du selbst erledigen

Dinge, die ich nicht für dich tun kann oder soll, weil sie Konten, Geld, Verträge oder Rechtsfragen betreffen.
Ich ergänze die Liste, sobald im Projekt etwas Neues dazukommt.

## Sofort
- [ ] Twitch Client Secret neu erzeugen, falls die alte `.env.example` irgendwo geteilt wurde (F-002)
- [x] GitHub CLI installiert, Anmeldung mit Fine-grained-Token nur für `pubpot`
- [ ] Token-Ablauf: ca. **24.02.2027** bei 90 Tagen. Das ist mitten im Next Fest, deshalb **vorher** erneuern (GitHub → Settings → Developer settings → Fine-grained tokens → Regenerate)
- [ ] GitHub → Settings → Emails: „Keep my email addresses private“ und „Block command line pushes that expose my email“ aktivieren

## Bis Phase 6 (CI) bzw. 8 (CD)
- [x] GitHub-Repo angelegt (öffentlich) und gepusht: https://github.com/TidragoUnity/pubpot
- [ ] GitHub Environments `staging` und `production` anlegen
- [ ] GitHub-Secrets eintragen (genaue Liste folgt in Phase 8)
- [ ] Branch-Schutz für `main`: PR mit grüner CI erforderlich

## Bis Phase 8 (Server) – vorher F-008 klären
- [ ] Hetzner VPS anlegen (F-008), SSH-Schlüssel hinterlegen
- [ ] VPS nach der Härtungsanleitung absichern (kommt in Phase 8)
- [ ] Domain `pubpot.games` registrieren
- [ ] DNS: A/AAAA-Einträge für `pubpot.games`, `www` und `staging` auf den VPS

## Bis zum Go-live
- [ ] SMTP-Anbieter in der EU wählen, Absenderdomain mit SPF, DKIM und DMARC einrichten
- [ ] Impressum und Datenschutzerklärung erstellen und **anwaltlich prüfen lassen**
- [ ] Einwilligungstext für die Formulare prüfen lassen (`Consent.*` in `SharedResource.resx`; bei Änderung `ConsentText.Version` erhöhen)
- [x] Aufbewahrungsfrist für bestätigte Anmeldungen festgelegt: 12 Monate Inaktivität, Warnmail, Löschung nach 30 Tagen (F-013, E-023)
- [ ] Diese Frist, die Warnmail (Texte `Mail.Retention.*` und `Mail.Kept.*` in `SharedResource.resx`) und den Abschnitt „Aufbewahrung“ der Datenschutzerklärung anwaltlich prüfen lassen
- [ ] Datenschutzerklärung: Notiz-Feld und Pipeline-Status im Admin-Bereich erwähnen (interne Akquise-Daten) und anwaltlich prüfen lassen
- [ ] Backup-Ziel wählen und buchen (F-008, noch offen) und einen Restore-Test selbst einmal durchführen
