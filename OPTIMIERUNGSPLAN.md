# Optimierungsplan – Autoklicker

Stand: 13. September 2026. Ursprünglich nur Planung; die anschließende Umsetzung wurde mit „leg los“ beauftragt. Ergebnisse werden in `OPTIMIERUNGSBERICHT.md` festgehalten.

## Ziel

Den vorhandenen Autoklicker sparsamer und intern einfacher machen. Klickrate, Hotkey, Start/Stopp, Speicherung und Bedienung bleiben erhalten. Das schwarze Design einschließlich der behobenen Fensterecken bleibt bestehen. Keine neuen Funktionen, Einstellungen, Hintergrunddienste oder zusätzlichen Produktabhängigkeiten.

## 1. Aktueller Befund

Quellcode geprüft: Anwendung und Instanzaktivierung, Fensterlogik, Klickausgabe, Hotkeys, Einstellungen und Projektdatei.

Die installierte App wurde ohne von mir ausgelöste Bedienung viermal im Abstand von ungefähr fünf Sekunden beobachtet. Prozess 7448, am 13.09.2026 von 15:15:54 bis 15:16:09 Uhr, lokale Zeit. Kein Neustart und kein aktiver Klicktest in dieser Planungsphase; der interne Start-/Stopp-Zustand wurde dabei nicht zusätzlich ausgelesen.

| Messwert | Beobachtung |
| --- | --- |
| Working Set, aktuell residente Speicherseiten einschließlich geteilter Seiten | konstant 174,48 MiB |
| Privater zugesicherter Prozessspeicher, nicht ausschließlich physischer RAM | konstant 114,99 MiB |
| Gesamte CPU-Zeit | konstant 12,46875 Sekunden; im Messfenster kein messbarer Zuwachs |
| Windows-Handles / Threads | konstant 1.659 / 19 |
| Installationsgröße auf dem Datenträger | 139,62 MiB, 402 Dateien |

Working Set und privater Speicher überlappen und dürfen nicht addiert werden. Die Installationsgröße ist kein RAM-Messwert. Threadzahlen enthalten auch Laufzeit- und WPF-Threads. Der kurze Ausschnitt belegt weder einen dauerhaft stabilen Speicherverbrauch noch ein Speicherleck. Aufteilung in .NET-Heap, Grafikressourcen und sonstigen nativen Speicher sowie Verhalten beim Klicken sind noch offen. Eine konkrete RAM-Einsparung lässt sich derzeit nicht seriös versprechen.

Bereits sinnvoll umgesetzt:

- Die Klickschleife wartet mit einem abbrechbaren Windows-Timer, ohne aktive Warteschleife. Sie läuft nur während des Klickens.
- Hotkeys und Aktivierung durch eine zweite Instanz sind ereignisgesteuert; keine regelmäßige Tastaturabfrage.
- Die Maus-Eingabestrukturen werden wiederverwendet. Der Status wird nicht bei jedem Klick neu gezeichnet.
- Änderungen am Regler werden bereits mit 400 ms Verzögerung gespeichert. Die Oberfläche hat keine dauerhaft laufende Statusanimation.

## 2. Kleine, konkrete Verbesserungen

### A. Ressourcen zuverlässig freigeben – zuerst

**Fundstelle:** `src/Autoklicker/App.xaml.cs`, `UserSuffix`, `ListenForActivation`, `OnExit`.

`UserSuffix` erzeugt bei jedem Zugriff eine neue `WindowsIdentity`, ohne sie ausdrücklich zu entsorgen. Auch jede erneute Pipe-Verbindung fragt die Identität wieder ab. Geplant: Benutzerkennung einmal bestimmen und die dafür verwendete Identität direkt freigeben. Microsoft dokumentiert `Dispose` als Freigabe der von `WindowsIdentity` verwendeten Ressourcen: [WindowsIdentity.Dispose](https://learn.microsoft.com/en-us/dotnet/api/system.security.principal.windowsidentity.dispose?view=net-10.0).

Den vorhandenen Aktivierungstask außerdem beim Beenden kontrolliert abbrechen und seine Ressourcen nach Ende freigeben. Die Abbruchausnahme während der Fehler-Verzögerung ebenfalls behandeln. Dabei kein synchrones Warten auf dem UI-Thread einführen, das mit einer ausstehenden Dispatcher-Aktion kollidieren könnte.

**Erwartung:** klare Ressourcenlebensdauer und robusteres Beenden. Die gemessene Handlezahl lässt sich diesem Befund noch nicht zuordnen; kein nachgewiesenes dauerhaftes Leck und kein großer RAM-Gewinn zugesichert.

### B. Nur tatsächliche Änderungen speichern

**Fundstellen:** `MainWindow.xaml.cs`, `SetRate`, `CommitRate`, `SaveSettings`, `OnClosed`; `Services/SettingsStore.cs`, `Save`.

Aktuell startet auch das Bestätigen einer unveränderten Klickrate den Speichertimer, beispielsweise beim Starten. Eine unmittelbar gespeicherte Hotkeyänderung kann anschließend nochmals über den noch laufenden Timer geschrieben werden. Beim Schließen wird immer gespeichert.

Geplant: letzte erfolgreich gespeicherte Werte vergleichen, identische Schreibvorgänge überspringen und einen bereits erledigten Speichertimer stoppen. Zahlenfeld-Normalisierung und Validierung trotzdem ausführen. Fehlgeschlagene Schreibvorgänge bleiben ausstehend und müssen später erneut versucht werden können. Beim ersten Lauf beziehungsweise nach einer beschädigten Datei weiterhin gültige Einstellungen anlegen.

Eine gemeinsame `JsonSerializerOptions`-Instanz verwenden. Microsoft empfiehlt deren Wiederverwendung, unter anderem wegen des darin gehaltenen Metadaten-Caches: [JsonSerializerOptions wiederverwenden](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options).

**Erwartung:** weniger Dateizugriffe und temporäre Objekte bei Bedienung; kleiner Aufwand. Atomarer Dateiaustausch und Fehlerbehandlung bleiben erhalten. Für die winzige Datei ist keine zusätzliche Speicher-Queue oder Datenbank vorgesehen.

### C. Vorhandene Darstellungsressourcen wiederverwenden

**Fundstelle:** `MainWindow.xaml.cs`, `UpdateControls`, `Brush`, `SetStatus`.

Derzeit werden Farben bei Statuswechseln erneut aus Text umgewandelt und Start-/Stopp-Symbole neu geparst. Geplant: die wenigen festen Brushes und Geometrien einmal anlegen beziehungsweise bestehende XAML-Ressourcen verwenden. Unveränderliche Ressourcen, soweit geeignet, einfrieren. Animierte Ressourcen davon ausnehmen. Grundlage: [WPF Freezable Objects](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/freezable-objects-overview).

**Erwartung:** weniger kurzlebige Objekte und übersichtlicherer Darstellungscode, vor allem beim Verstellen und Umschalten. Da das nicht pro Klick passiert, ist der Effekt begrenzt. Farben, Schrift, Animationen und Fensterrundung bleiben optisch gleich.

## 3. Umsetzung in vier überschaubaren Schritten

| Schritt | Arbeit | Fertig, wenn … |
| --- | --- | --- |
| 1. Vergleichsbasis | Identischen Release-Build prüfen; drei vergleichbare Starts mit kurzer Aufwärmzeit messen. Anschließend Ruhephase, minimierte App und 1/10/50/100 Klicks pro Sekunde in einer lokalen Testfläche beobachten. | CPU, Working Set, privater Speicher, Handles und Threads samt Messdauer dokumentiert sind. |
| 2. Kleine Korrekturen | A und B getrennt umsetzen, anschließend C. Bestehende Dienste und Zustände beibehalten. | Ressourcenfreigabe, Speichervorgänge und wiederverwendete Darstellung nachvollziehbar sind. |
| 3. Wirkung und Verhalten prüfen | Vergleichsmessungen unter gleichen Bedingungen; wiederholte Start-/Stopp-Zyklen und Aktivierung durch zweite Instanz. | Verhalten unverändert funktioniert und keine reproduzierbare Verschlechterung auftritt. |
| 4. Abschließen | Ergebnisse mit Vorher-/Nachher-Werten festhalten, Release bauen und vorhandene Installation aktualisieren. | Desktop-Verknüpfung funktioniert und die fertige App geöffnet ist. |

Schritte 1–4 beschreiben die anschließend beauftragte Umsetzung. Keine neue Anzeige für Leistungswerte in der App.

## 4. Sinnvolle Prüfung und Entscheidung über weitere Arbeit

- **Ruhe:** nach Aufwärmen über mindestens 60 Sekunden beobachten. CPU als Zuwachs der Prozess-CPU-Zeit pro realer Zeit ausweisen; Bezugsgröße ausdrücklich angeben.
- **Klicken:** jeweils 30 Sekunden bei 1, 10, 50 und 100 Klicks/s auf einer kontrollierten lokalen Testfläche. Rate weiterhin möglichst innerhalb ±5 %, vollständige Drücken-/Loslassen-Paare, Stopp weiterhin unter 100 ms bei normaler Last und keine späteren Klicks nach abgeschlossenem Stopp.
- **Lebensdauer:** drei Blöcke von jeweils 100 Start-/Stopp-Zyklen und jeweils 20 Aktivierungen durch eine zweite Instanz; danach Ruhephase. Nach dem Aufwärmen darf kein reproduzierbarer, mit jedem Block weiter wachsender Ressourcenbestand entstehen. Ein einmal höherer Speicherstand allein gilt nicht als Leckbeweis.
- **Gezielte Tests für B:** unveränderte Werte, mehrere schnelle Änderungen, fehlende/defekte Datei, Schreibfehler mit anschließendem erfolgreichem Wiederholen sowie letzte Änderung unmittelbar vor dem Schließen.
- **Bedienung:** Hotkey inklusive Konflikten, Sperre/Standby, Minimieren, Zahlenfeld und Fensterverschieben weiterhin prüfen. Native Fensterecken vor hellem und dunklem Hintergrund prüfen; den bekannten Fix erhalten.

Wenn die Vergleichsmessungen ein anhaltendes Speicherwachstum zeigen, erst dann gezielt Heap und native Ressourcen untersuchen. Speicheraufnahmen nur lokal und außerhalb des ausgelieferten Programms. Weitere Optimierungen nur bei einem identifizierten Verursacher. Wenn die App stabil bleibt und weitere Einsparungen nur einen großen Umbau erlauben, endet die Arbeit nach den kleinen Korrekturen.

## 5. Bewusst begrenzter Umfang

Kein Framework-Wechsel, keine neue Architektur mit zusätzlichen Schichten, kein dauerhaft bereitgehaltener Klickthread, keine Änderung am bewährten Timer ohne Messgrund. Kein künstliches Erzwingen der Garbage Collection oder Leeren des Working Sets, um die RAM-Anzeige kleiner aussehen zu lassen. Paketkomprimierung, Single-File, Trimming und AOT sind nicht Teil dieses Plans. Die enthaltene Laufzeit bleibt für den bisherigen unkomplizierten App-Start erhalten.

**Empfehlung:** ein kleiner Wartungsdurchgang mit Messung, Ressourcenbereinigung, weniger Speichervorgängen und wiederverwendeten UI-Ressourcen. Die Größenordnung eines möglichen RAM-Gewinns wird erst nachher aus Vergleichsmessungen angegeben.
