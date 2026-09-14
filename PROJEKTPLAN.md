# Projektplan – Autoklicker

Stand: 13. September 2026. Ursprünglicher Projektplan; Umsetzung anschließend vom Nutzer beauftragt. Aktueller Liefer- und Prüfstatus: siehe `PRUEFBERICHT.md`.

## 1. Ziel und Rahmen

Eine eigenständige Windows-App namens **Autoklicker** mit kleinem, schwarzem Fenster, einstellbarer Klickrate und frei belegbarem globalem Start-/Stopp-Hotkey. Die fertige App erhält eine Desktop-Verknüpfung namens **Autoklicker** mit eigenem Symbol.

Ausgangslage bei der Planung: Der Projektordner enthielt nur die Git-Verwaltung; installiert war das .NET SDK 9.0.316. In der damaligen Planungsphase wurden weder Programmcode noch Installationen oder eine funktionslose Startverknüpfung erstellt.

## 2. Geplante Bedienung

| Element | Geplantes Verhalten |
| --- | --- |
| Fenster | Etwa 340 × 300 logische Pixel, verschiebbar, minimierbar, mit Schließen-Schaltfläche; skaliert mit der Windows-Anzeigeskalierung. |
| Klickrate | 1–100 Klicks pro Sekunde, Standard 10. Horizontaler Regler mit Einrasten in Schritten von 1; sichtbare Hauptmarkierungen bei 1, 25, 50, 75 und 100. |
| Exakter Wert | Zahlenfeld neben der Beschriftung; synchron zum Regler, auch mit Pfeiltasten bedienbar. Ungültige Werte erhalten eine kurze Meldung. |
| Hotkey | Feld „Hotkey ändern“ anklicken und gewünschte Taste oder Kombination drücken; beispielsweise F6 oder Strg + Alt + K. Derselbe Hotkey schaltet ein und aus, auch bei minimiertem Fenster. |
| Start/Stopp | Eine große Schaltfläche und eine eindeutige Statusanzeige „Bereit“, „Startet …“ oder „Aktiv“. |
| Klickziel | Linksklick an der jeweils aktuellen Mausposition. Ein Klick besteht aus Drücken und Loslassen. |
| Speichern | Klickrate und Hotkey werden lokal gespeichert; die App startet grundsätzlich im gestoppten Zustand. |

**Planungsannahme:** „Raster“ bedeutet einen Schieberegler mit festen Raststufen. Wertebereich, Fenstergröße und Standardtaste sind vorgeschlagene Ausgangswerte.

Beim Start über die Schaltfläche gibt es eine kurze Vorlaufzeit von einer Sekunde, damit der Mauszeiger zum Ziel bewegt werden kann. Der Hotkey startet direkt. Ein erneuter Start-/Stopp-Befehl während der Vorlaufzeit bricht den Start ab.

Während des Klickens und der Vorlaufzeit sind die Einstellungen gesperrt; Stoppen bleibt immer möglich. Befindet sich der Zeiger über dem eigenen Fenster, werden automatische Klicks dort unterdrückt. Beim Verlassen geht das Klicken weiter, sofern es noch aktiviert ist. Schließen, Windows-Sperre oder Standby stoppen die Ausgabe; es erfolgt kein automatischer Neustart danach.

## 3. Gestaltung

Eigenständiger, reduzierter Entwurf: fast schwarzer Hintergrund (#0B0B0D), dunkelgraue Eingabeflächen (#17171A), dezente Konturen (#303036), helle Schrift (#F2F2F4) und graue Nebenbeschriftungen. Kleine abgerundete Ecken und klare Abstände; Status wird durch Text und einen kleinen Punkt erkennbar. Bedienelemente erhalten sichtbare Tastatur-Fokusmarkierungen.

Anordnung von oben nach unten:

1. App-Name, Minimieren und Schließen.
2. „Klicks pro Sekunde“ mit Zahlenfeld, darunter der gerasterte Regler.
3. „Start-/Stopp-Hotkey“ mit aktueller Belegung und Änderungsmöglichkeit.
4. Breite Start-/Stopp-Schaltfläche und kompakte Statuszeile.

Keine zusätzlichen Seiten nötig. Die erste Version konzentriert sich auf Linksklicks, Klickrate und Hotkey. Makros, Profile und weitere Klickmodi wären spätere Erweiterungen.

## 4. Technische Umsetzung

- **C# und WPF:** native Windows-Desktopoberfläche mit frei gestaltbaren Bedienelementen und Unterstützung für Anzeigeskalierung. Grundlage: [Microsoft WPF-Übersicht](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/).
- **.NET 10 LTS als Ziel:** vor der Umsetzung das passende SDK bereitstellen. .NET 10 ist laut [Microsoft-Supportübersicht](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) bis November 2028 unterstützt. Das vorhandene SDK 9 genügt nicht zum Bauen dieses Zielprojekts.
- **Globale Hotkeys:** Windows `RegisterHotKey` mit `MOD_NOREPEAT`, damit Gedrückthalten nicht mehrfach umschaltet. Bereits belegte und reservierte Kombinationen abfangen; F12, Windows-Kombinationen und reine Modifikatortasten nicht anbieten. Standard F6 nur übernehmen, wenn verfügbar. Schlägt eine Änderung fehl, bleibt die vorherige gültige Belegung erhalten. Quelle: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
- **Mausausgabe:** Windows `SendInput` sendet vollständige Links-Drücken/Loslassen-Paare. Windows kann Eingaben in höher privilegierte Programme blockieren; eine universelle Funktion in allen Anwendungen ist deshalb nicht zugesichert. Quelle: [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).
- **Zeitsteuerung:** eigene abbrechbare Hintergrundschleife mit monotoner Zeitmessung und geeigneter Windows-Wartefunktion. Intervall = 1.000 ms / Klickrate. Keine dauerhafte aktive Warteschleife; verpasste Intervalle nicht als nachträglichen Klickschwall ausgeben. Windows ist kein Echtzeitsystem; die tatsächlich erreichbare Rate wird gemessen.
- **Sauberer Stopp:** vor jeder Ausgabe den Abbruchzustand prüfen, ausstehende Wartezeit unterbrechen und den Worker beenden. Starten darf nie mehrere Klickschleifen erzeugen. Hotkeys beim Beenden freigeben; zweite App-Instanz bringt das vorhandene Fenster nach vorne.
- **Lokale Einstellungen:** kleine JSON-Datei unter `%LOCALAPPDATA%\Autoklicker`, validiert und atomar gespeichert. Beschädigte Einstellungen führen zu Standardwerten und einer kurzen Meldung.
- **Auslieferung:** eigenständiges Windows-Paket mit enthaltener .NET-Laufzeit, damit für die Nutzung keine separate Laufzeitinstallation nötig ist. Architektur vor dem Build prüfen. Grundlage: [Microsoft zur Veröffentlichung von .NET-Apps](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

Geplante Bausteine: Fenster und Anwendungszustand, Klickdienst, Hotkey-Dienst, Einstellungsdienst sowie Paketierung und Verknüpfung. Alle Windows-Eingriffe bleiben in klar abgegrenzten Diensten.

## 5. Arbeitsschritte nach Beginn der Umsetzung

| Phase | Arbeit | Fertiges Ergebnis |
| --- | --- | --- |
| 1. Grundlage | Betriebssystem/Architektur prüfen, .NET-10-SDK bereitstellen, WPF-Projekt anlegen. | App startet mit korrektem Namen als kleines Fenster. |
| 2. Oberfläche | Schwarzes Design, Regler, Zahlenfeld, Hotkey-Aufnahme, Status und Start/Stopp umsetzen. | Bedienbare Oberfläche in der vorgesehenen Größe. |
| 3. Funktion | Klickdienst, globale Hotkeys, Abbruch und Schutz vor Mehrfachstart integrieren. | Einstellbare Klickrate; Start und Stopp funktionieren auch minimiert. |
| 4. Robustheit | Speicherung, Hotkey-Konflikte, Fensterschutz, Sperre/Standby und zweite Instanz behandeln. | Vorhersagbares Verhalten auch in Fehlerfällen. |
| 5. Prüfung | Verhalten und Timing in einer kontrollierten lokalen Klick-Testfläche prüfen; Darstellung bei unterschiedlicher Skalierung kontrollieren. | Dokumentierte Prüfergebnisse und behobene Fehler. |
| 6. Auslieferung | Release-Paket, eigenes schlichtes Symbol, kurze deutsche Anleitung und Desktop-Verknüpfung erstellen. | Fertige App per Doppelklick startbar. |

Die veröffentlichte App liegt später an einem stabilen Ort unter `%LOCALAPPDATA%\Programs\Autoklicker`. Die Desktop-Verknüpfung wird über den tatsächlichen Windows-Desktopordner angelegt, damit auch ein umgeleiteter Desktop berücksichtigt wird. Sie verweist auf die fertige `Autoklicker.exe`, nicht auf einen temporären Buildordner. Bestehende gleichnamige fremde Dateien werden nicht überschrieben.

## 6. Abnahme und Tests

- Regler und Zahlenfeld stimmen bei 1, 10, 50 und 100 Klicks/s überein; leere und ungültige Eingaben werden kontrolliert behandelt.
- Gemessene Klickzahlen liegen über jeweils 30 Sekunden im unbelasteten Test möglichst innerhalb von ±5 % der Sollrate. Größere Abweichungen müssen vor Auslieferung untersucht werden.
- Nach verarbeitetem Stopp entstehen keine neuen Klickpaare. Angestrebte Reaktion unter normaler Last: innerhalb von 100 ms, auch bei 1 Klick/s.
- Der frei gewählte Hotkey startet und stoppt bei fokussierter anderer App sowie minimiertem Autoklicker; Gedrückthalten schaltet nur einmal.
- Hotkey-Konflikte, Abbrechen der Hotkey-Aufnahme und wiederholte schnelle Start-/Stopp-Eingaben hinterlassen einen konsistenten Zustand.
- Über dem eigenen Fenster werden keine automatischen Klicks ausgelöst. Schließen, Sperren und Standby beenden das Klicken zuverlässig.
- Einstellungen überstehen einen Neustart; eine defekte Einstellungsdatei verhindert den Start nicht.
- Bei 100 %, 150 % und 200 % Windows-Skalierung bleiben Texte und Schaltflächen vollständig sichtbar und bedienbar.
- Die Desktop-Verknüpfung startet die veröffentlichte App ohne Terminalfenster; zweite Instanz und Beenden hinterlassen keine weitere Klickschleife.

Automatisierte Tests konzentrieren sich auf Validierung, Zustandswechsel und Zeitberechnung. Globale Hotkeys und echte Mausausgabe werden zusätzlich praktisch überprüft. Falls die verfügbare Testumgebung einzelne Windows-Prüfungen nicht zulässt, werden diese ausdrücklich als offen dokumentiert.

## 7. Geplante Übergabe

Quellprojekt, lauffähiges Release-Paket, Desktop-Verknüpfung **Autoklicker**, kurze Bedienungsanleitung und Prüfergebnisse. Die Umsetzung wurde nach Erstellung dieses Plans vom Nutzer beauftragt.
