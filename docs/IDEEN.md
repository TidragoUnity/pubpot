# Ideen für später (nach V1)

Hier sammle ich Ideen für die eigentliche Plattform. **Nichts davon ist geplant oder gebaut.** V1 ist nur die Landingpage mit Anmeldung (siehe [ROADMAP.md](ROADMAP.md)).

Wenn eine Idee dran ist, so vorgehen:
1. Den Abschnitt hier zu einem Entwurf ausarbeiten (Status 📝).
2. Offene Punkte als Rückfragen in [RUECKFRAGEN.md](RUECKFRAGEN.md) klären.
3. Die Idee als Phase in der Roadmap planen (Status 🗺️).

| Symbol | Bedeutung |
|---|---|
| 💡 | Idee, nur notiert |
| 📝 | Entwurf in Arbeit |
| 🗺️ | in der Roadmap geplant |
| ❌ | verworfen (mit Begründung) |

## Übersicht

| Nr | Idee | Status |
|---|---|---|
| I-01 | Creator sehen, wer ein Spiel noch bewirbt, und bilden Gruppen | 💡 |
| I-02 | Entwickler legen ihre Pools zu einem Bündel zusammen | 💡 |
| I-03 | Steam-Keys direkt an Creator ausgeben | 💡 |
| I-04 | Spielekatalog wie eine Steam-Seite, mit Teilnehmern und Pot-Größe | 💡 |

**Mögliche Reihenfolge:** I-04 zuerst, denn ohne Spieleseiten gibt es nichts, worauf sich die anderen Ideen beziehen. Danach I-01 und I-03. I-02 ist rechtlich und beim Abrechnen am aufwendigsten, deshalb zuletzt.

---

## I-01 · Creator sehen, wer ein Spiel noch bewirbt 💡

**Idee:** Ein Streamer sieht, welche anderen Creator dasselbe Spiel bewerben. So können sich Gaming-Gruppen bilden, z. B. für gemeinsame Streams, Raids oder Turniere.

**Wozu:** Kleine Creator bekommen Kontakte und mehr Reichweite. Ein Spiel wirkt größer, wenn mehrere Creator gleichzeitig darüber sprechen.

**Erste Gedanken zur Umsetzung**
- Auf jeder Spieleseite (I-04) eine Liste „Diese Creator sind dabei“.
- Sichtbar ist nur, wer zugestimmt hat (Opt-in im Profil, jederzeit widerrufbar).
- Angezeigt werden nur Kanalname und Plattform, keine E-Mail-Adressen.
- Der Twitch-Worker sammelt schon Stream-Daten. Ob sich daraus ablesen lässt, wer ein Spiel gerade live spielt, ist zu prüfen.

**Offen / zu prüfen**
- Wie nehmen Creator Kontakt auf: über einen Link zum eigenen Kanal, ein eigenes Nachrichtensystem oder einen Discord-Server pro Spiel? Ein eigenes Nachrichtensystem bringt Pflichten mit, z. B. Moderation und Meldewege nach dem Digital Services Act (DSA).
- Datenschutz: Welche Profildaten dürfen öffentlich sein (DSGVO, Einwilligung)?

**Hängt ab von:** Creator-Konten, I-04.

---

## I-02 · Entwickler bündeln ihre Pools 💡

**Idee:** Mehrere Entwickler legen sich zusammen, damit der Pool größer wird. Wird eines der Spiele erfolgreich, bekommen alle etwas. Anders gesagt: Kauft jemand eines der Spiele, profitieren alle.

**Wozu:** Das Risiko verteilt sich, denn nicht jedes Spiel wird ein Erfolg. Für Creator ist ein Bündel attraktiver, weil der Pot größer und sicherer ist.

**Erste Gedanken zur Umsetzung**
- Ein „Bündel“ fasst mehrere Spiele mit einem gemeinsamen Pool zusammen.
- Jedes Spiel zahlt seinen Anteil wie bisher (z. B. 25 % für 3 Monate) in den gemeinsamen Topf.
- Creator, die ein Spiel des Bündels spielen, teilen sich den gemeinsamen Topf.

**Offen / zu prüfen**
- **Wer profitiert genau?** Zwei Lesarten:
  - (a) Nur die Creator profitieren vom gemeinsamen Topf.
  - (b) Auch die anderen Entwickler bekommen etwas vom erfolgreichen Spiel. Dann wäre es eine Art gegenseitige Absicherung.
  - Das ist zu entscheiden.
- Bei Lesart (b) zahlen Firmen untereinander Geld aus. Das braucht Verträge und eine steuerliche Prüfung. Zu klären ist auch, ob das als Versicherung oder als Glücksspiel gelten könnte. Vorher rechtlich beraten lassen.
- Wie wird fair aufgeteilt, wenn ein Spiel 90 % des Umsatzes bringt?
- Wer darf in ein Bündel (Genre, Größe, Launch-Zeitraum)?

**Hängt ab von:** Abrechnung des normalen Pools, I-04.

---

## I-03 · Steam-Keys direkt an Creator 💡

**Idee:** Ein Creator, der ein Spiel bewerben will, bekommt sofort einen Steam-Key, ohne beim Entwickler nachfragen zu müssen.

**Wozu:** Es gibt keine Wartezeit und keinen Mailverkehr. Entwickler müssen nicht jeden Creator einzeln prüfen.

**Erste Gedanken zur Umsetzung**
- Den Begriff „Generator“ nur mit Vorsicht verwenden: Soweit ich weiß, kann man Steam-Keys nicht über eine öffentliche API selbst erzeugen. Entwickler beantragen sie in Steamworks in Stapeln, für große Mengen mit Freigabe durch Valve. Das ist zu prüfen.
- **Variante A, Key-Vorrat:** Der Entwickler lädt eine Liste von Keys hoch. PubPot gibt jedem freigeschalteten Creator genau einen Key.
- **Variante B, Steam Curator Connect:** Darüber können Entwickler ein Spiel direkt an Kuratoren und Content Creator schicken, ganz ohne Keys. Ob und wie sich das anbinden lässt, ist zu prüfen.
- Keys sind wertvoll wie Passwörter:
  - Sie werden verschlüsselt gespeichert und erscheinen nie im Log.
  - Jede Ausgabe wird protokolliert.

**Offen / zu prüfen**
- **Missbrauch:** Key-Wiederverkäufer geben sich gern als Streamer aus. Ein Schutz wäre, Keys nur an verifizierte Kanäle auszugeben, z. B. mit Twitch-Login und einer Mindestzahl an Streams. Dazu ein Limit pro Zeitraum.
- Was passiert, wenn der Vorrat leer ist? Dann bekommt der Entwickler eine Benachrichtigung und der Creator eine Warteliste.
- Die Regeln von Steam zur Key-Weitergabe sind zu prüfen.

**Hängt ab von:** Creator-Konten mit Kanal-Verifizierung, I-04.

---

## I-04 · Spielekatalog mit Teilnehmern und Pot 💡

**Idee:** Eine Übersicht wie eine Steam-Seite zeigt alle aktuellen Spiele, für die man werben kann. Zu jedem Spiel gibt es ein kleines Kreisdiagramm, wie viele Creator schon dabei sind, und die Größe des Pots.

**Wozu:** Creator sehen auf einen Blick, wo sich Mitmachen lohnt. Entwickler sehen, wie viel Interesse ihr Spiel weckt.

**Erste Gedanken zur Umsetzung**
- **Katalog:** Kacheln mit Titelbild, Genre, Launch-Zeitraum und Anteil (z. B. 25 % für 3 Monate).
- **Spieleseite:** Beschreibung, Bilder vom Entwickler und ein Link zur Steam-Seite. Steam-Logos und das Steam-Design werden nicht nachgebaut (Markenrecht), nur das Prinzip.
- **Kreisdiagramm:**
  - Es zeigt belegte gegenüber freien Plätzen, falls es ein Limit gibt, oder die Anzahl der Teilnehmer.
  - Die Zahl steht immer auch als Text daneben (Barrierefreiheit).
  - Es wird mit eigenem SVG gezeichnet, ohne externe Bibliothek. So bleibt es mit der CSP verträglich.
- **Pot:**
  - Vor dem Launch wird eine Schätzung angezeigt, klar als Schätzung gekennzeichnet (wie das Rechenbeispiel auf der Landingpage).
  - Nach dem Launch werden echte Zahlen angezeigt.

**Offen / zu prüfen**
- Woher kommen die Umsatzzahlen? Entweder meldet der Entwickler sie selbst, oder sie kommen automatisch aus den Verkaufsdaten von Steamworks (Partner-API, zu prüfen).
- Wollen Entwickler ihren Umsatz öffentlich zeigen? Falls nicht, nur eine Spanne anzeigen, z. B. „Pot: 500 – 1.000 €“.
- Gibt es ein Limit an Creatorn pro Spiel? Je mehr Creator dabei sind, desto kleiner wird der Anteil für jeden.

**Hängt ab von:** Entwickler-Konten, Freigabe von Spielen durch den Admin.

---

## Vorlage für neue Ideen

```markdown
## I-0xx · Titel 💡

**Idee:** …
**Wozu:** …
**Erste Gedanken zur Umsetzung**
- …
**Offen / zu prüfen**
- …
**Hängt ab von:** …
```
