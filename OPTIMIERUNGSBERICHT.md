# Autoklicker – Optimierung

Stand: 13. September 2026. Umsetzung des anschließend beauftragten `OPTIMIERUNGSPLAN.md`.

## Änderungen

- Benutzerkennung einmal ermitteln und die dafür verwendete Windows-Identität sofort freigeben. Pipe-Task beim Beenden abbrechen, ausstehende UI-Aktivierung ebenfalls abbrechbar machen und die Abbruchquelle nach Task-Ende entsorgen. Kein blockierendes Warten auf dem UI-Thread.
- Einstellungen mit dem letzten erfolgreich gespeicherten Stand vergleichen. Identische Werte werden nicht nochmals geschrieben; fehlgeschlagene Änderungen bleiben erneut speicherbar. Auch nach beschädigten oder fehlenden Dateien werden gültige Standardwerte wieder angelegt.
- Den Speichertimer nur bei geänderter Klickrate starten und nach erfolgreichem sofortigem Speichern stoppen. Zahlenfeld weiterhin normalisieren. Atomare Speicherung erhalten, JSON-Konfiguration wiederverwenden.
- Acht feste graue Brushes und zwei Symbol-Geometrien einmal erstellen und eingefroren wiederverwenden. Keine erneute Farbumwandlung oder Symbolanalyse bei jedem Statuswechsel.

Unverändert per Dateihash: `MainWindow.xaml`, `App.xaml`, `WindowFrame.cs`, `Services/ClickEngine.cs`, `Services/HotkeyService.cs`. Keine neuen Produktfunktionen oder Bibliotheken. Die Testwerkzeuge sind nicht Teil der installierten App.

## Funktionstests

Der vollständige Testlauf in `artifacts/optimization/tests.log` endet mit **ALLE TESTS BESTANDEN**. Acht gezielte Optimierungs-/Zustandstests und zehn bestehende Prüfungen bestanden.

| Rate | Gezählt | Messdauer | Abweichung |
| --- | ---: | ---: | ---: |
| 1/s | 31 | 30,006 s | 3,31 % |
| 10/s | 301 | 30,008 s | 0,31 % |
| 50/s | 1.501 | 30,006 s | 0,05 % |
| 100/s | 3.000 | 30,010 s | 0,03 % |

Stopp bei 1/s: **0,11 ms**. Keine spätere Ausgabe nach Stopp. Die Zeitmessungen zählen die Ausgabe des produktiven Klickdienstes ohne reale Mausausgabe.

Zusätzlich geprüft: identische Einstellungen auch nach erneutem Laden, Schreibfehler mit erfolgreicher Wiederholung, Rückkehr zum zuvor gespeicherten Wert, temporäre Dateien, defekte/fehlende Einstellungen, mehrere schnelle Änderungen, Normalisierung ohne Schreibvorgang, letzte Änderung direkt beim Schließen, erledigter Speichertimer, Abbruch einer Pipe ohne Nutzdaten sowie Abbruch bei blockierter UI-Aktivierung. Vorlaufabbruch und Sitzungs-/Energiesignale wurden mit einer zählenden Testausgabe geprüft.

## Vergleichsmethode

Die alte installierte Anwendungs-DLL wurde vor Änderungen gesichert; SHA-256: `6B49D8B0AC59D49A4C0C3E245D573D7C191FD3C2B076702AB9091CA0AF428A21`.

Zusätzlich zu drei Starts der jeweils installierten App werden beide DLLs mit demselben isolierten Testprogramm verglichen: fünf Sekunden Aufwärmen, 60 Sekunden Ruhe, 30 Sekunden minimiert, jeweils 30 Sekunden bei 1/10/50/100 Ausgaben pro Sekunde sowie drei Blöcke mit je 100 Start-/Stopp-Zyklen und 20 Pipe-Aktivierungen. Die Klickausgabe ist dabei ein Zähler. Testeinstellungen liegen in einem eigenen temporären Ordner.

Der Testhost lädt die identischen XAML-Ressourcen zur Laufzeit und enthält Messcode. Seine absoluten Speicherwerte dürfen deshalb nicht mit dem RAM-Verbrauch der installierten App gleichgesetzt werden. Die CPU-Angaben beziehen sich auf einen logischen Kern: Prozess-CPU-Zuwachs geteilt durch reale Messdauer, mal 100. Working Set und privater zugesicherter Speicher sind verschiedene, überlappende Messgrößen.

Die ersten langen Testhost-Läufe wurden verworfen: Ein zusätzlich ausgelöster normaler WPF-App-Start verfälschte Instanzprüfung und Ressourcenmessung. Der isolierte Test-App-Start verhindert das jetzt. Ein doppeltes Freigeben des testweise eingesetzten Klickdienstes wurde ebenfalls in der Testumgebung korrigiert. Die Rohdateien `before-profile.json` und `after-profile.json` enthalten die korrigierten vollständigen Läufe.

## Direkt gemessene Wirkung

Eine zusätzliche Messung führt die betroffenen Vorgänge synchron auf dem UI-Thread aus, ohne Fensterdarstellung oder Dispatcher-Pumpen. Die identische Testdatei wird jeweils mit alter beziehungsweise neuer Anwendungs-DLL gestartet. Erfasst werden neu angelegte verwaltete Bytes des ausführenden Threads, nicht der dauerhaft belegte RAM. Kein erzwungener Garbage-Collector-Lauf.

| Synthetischer Test | Vorher | Nachher |
| --- | ---: | ---: |
| 1.000 Statuswechselpaare Aktiv/Bereit: neu angelegte Bytes | 6.072.000 | 2.824.000 |
| 100 identische Speicheraufrufe nach erster erfolgreicher Speicherung: neu angelegte Bytes | 122.816 | 0 |
| Dieselben 100 identischen Speicheraufrufe: Dauer | 45,54 ms | 0,08 ms |

Damit entstehen beim isolierten Statuswechseltest **53,5 % weniger temporäre verwaltete Bytes**. Identische Speicheraufrufe verursachen nach der ersten erfolgreichen Speicherung keine neuen Dateischreibvorgänge. Das bedeutet ausdrücklich nicht 53,5 % weniger RAM für die gesamte App. Rohdaten: `artifacts/optimization/before-allocations.jsonl` und `after-allocations.jsonl`.

Die Starts und langen Fenstervergleiche schwanken deutlich mit Fokus, Hover/Tooltip und der laufenden Windows-UI-Automation. Eine ergänzende .NET-Thread-Stichprobe zeigt unter anderem `ElementProxy`, `WM_GETOBJECT` und Dispatcher-Aufrufe; diese Diagnose liefert keinen eindeutigen Nachweis, dass der Zahlenfeld-Cursor die Ursache ist. Deshalb wurde das Fokusverhalten nicht auf Verdacht verändert. Die Stichprobe misst Thread-Verweildauer inklusive Wartezeiten, keine reine CPU-Zuordnung. Eine große pauschale RAM- oder CPU-Ersparnis wird daraus nicht abgeleitet. Die Diagnosewerkzeuge und lokalen Traces werden nicht mit ausgeliefert. Werkzeugbeschreibung: [Microsoft dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

## Speichervergleich und Langlauf

Beide korrigierten Vergleichsläufe wurden vollständig mit Exitcode 0 abgeschlossen. Ihre Test-DLL war identisch (SHA-256 `E11BC7C7430E33DC29B004D0E3EA462EBEDE09455E97FEFDBFB1EF9AF11EFA59`); nur die Anwendungs-DLL wurde getauscht.

| Zustand des Testhosts | Working Set vorher / nachher | Privater Speicher vorher / nachher |
| --- | ---: | ---: |
| Nach 60 Sekunden Ruhe | 181,26 / 184,47 MiB | 120,36 / 121,98 MiB |
| Nach 100 Ausgaben/s | 189,12 / 189,79 MiB | 125,30 / 123,71 MiB |
| Nach allen Umschalt-/Aktivierungsblöcken und abschließender Ruhe | 197,45 / 195,50 MiB | 128,35 / 127,77 MiB |

Nach den drei Belastungsblöcken lag der private Speicher der neuen Version bei 133,92 → 125,98 → 127,81 MiB; die Handle-Zahlen lagen bei 2.253 → 2.253 → 2.261. Kein fortlaufender Anstieg über die Blöcke. Das ist eine begrenzte Belastungsprüfung und kein Beweis, dass unter beliebigen Bedingungen niemals ein Leck auftreten kann.

Bei drei Starts der installierten App lag das Working Set am Ende der jeweiligen kurzen Messung vorher zwischen 154,27 und 179,44 MiB, nachher zwischen 169,98 und 178,42 MiB. Fokus, Hover und Tooltip waren in dieser Desktop-Umgebung nicht in allen Starts identisch. Daraus ergibt sich **keine belastbare große RAM-Einsparung**. Auch die langen Messungen zeigen im Wesentlichen denselben Speicherbedarf.

Die CPU-Werte des sichtbaren Testfensters werden wegen der beschriebenen UI-Automation und zusätzlicher Trace-Aufnahmen nicht als sauberer Vorher-/Nachher-Beleg verwendet. Die ungestörten minimierten 30-Sekunden-Abschnitte lagen bei 0,00 % beziehungsweise 0,10 % eines logischen Kerns. Die produktive Klickschleife wurde unverändert beibehalten.

## Abschließende Windows-Prüfung und Lieferung

- Eigene Belegung **Strg + Alt + K** aufgenommen; Start und Stopp bei fokussierter lokaler Testfläche erfolgreich.
- Sichtbarer Autoklicker: **2.228 automatische Klickpaare** zusätzlich zu einem manuellen Vorbereitungsklick. Nach Stopp blieb der Zähler bei 2.229 Drücken und 2.229 Loslassen.
- Minimierter Autoklicker: weitere **1.332 automatische Klickpaare**. Endstand 3.562 Drücken und 3.562 Loslassen, nach Stopp unverändert. Die manuell gesteuerten UI-Läufe belegen vollständige Paare und Hotkey-Bedienung; sie sind keine präzisen 100-Hz-Durchsatzmessungen. Die Rate wurde separat mit dem zählenden Klickdienst gemessen.
- Zweiter App-Start beendet sich mit Exitcode 0 und bringt die vorhandene minimierte Instanz zurück; deren PID bleibt erhalten.
- Native Fensterecken vor weißem und dunkelgrauem Hintergrund geprüft: keine rechteckigen grauen Eckreste; der normale Windows-Schatten bleibt erhalten.
- Freie Fensterfläche verschiebt das Fenster; Ziehen im Zahlenfeld markiert die Zahl und verschiebt das Fenster nicht. Regler und Zahlenfeld synchron.
- Einstellungen wieder auf **10 Klicks/s und F6** gesetzt. App zur Übergabe erneut über die Desktop-Verknüpfung geöffnet, im gestoppten Zustand.
- Beim abschließenden Schließen und erneuten Öffnen blieb der Änderungszeitpunkt der unveränderten Einstellungsdatei identisch. Ihr SHA-256 entspricht wieder dem ursprünglichen Stand; genau eine App-Instanz ist geöffnet.
- Release-Build ohne Fehler und Warnungen. Vorhandene Installation und Desktop-Verknüpfung aktualisiert; EXE und DLL werden durch das Installationsskript per SHA-256 gegen das Release geprüft.

Tatsächliche Windows-Sperre/Standby und physisches Gedrückthalten eines Hotkeys wurden auf dem laufenden Arbeitsplatz nicht ausgelöst. Sperr-/Energiesignale und Vorlaufabbruch sind durch die gezielten Zustandstests abgedeckt; Hotkey-Konflikte und Freigabe durch die Windows-Registrierungstests.

## NSIS-Release

Das selbstständige Release wurde erneut veröffentlicht und mit NSIS 3.12 als per-user Installer verpackt. Ausgabe: `artifacts\Autoklicker-Setup-1.0.0-x64.exe`, 59,80 MiB, SHA-256 `51E5CB3643EB8EAA299C3A83753751EA6706FAB6C440EEBE34834E7BDB698160`. Die Verpackung enthält die selbstständige .NET-Laufzeit, legt Desktop- und Startmenü-Verknüpfungen an und löscht die Einstellungen bei einer Deinstallation nicht. Der Installer wurde erfolgreich kompiliert; ein Testlauf mit dem Installationsprogramm selbst bleibt bewusst aus, damit die vorhandene Desktop-Verknüpfung und die laufende Benutzerinstallation nicht umgebogen werden.

**Ergebnis:** weniger vermeidbare Allokationen und Dateizugriffe, saubere Freigabe der Windows-Identität und kontrollierter Pipe-Abbruch. Grundfunktionen und Design bleiben erhalten. Der gesamte RAM-Bedarf bleibt ungefähr auf dem bisherigen Niveau; ein größerer Architekturumbau ist für diese Wartung nicht gerechtfertigt.
