# Autoklicker

Kleine schwarze Windows-App für automatische Linksklicks. Das Fenster ist 340 × 300 logische Pixel groß. Die App arbeitet lokal.

## Bedienung

1. **Autoklicker** auf dem Desktop öffnen.
2. **Klicks pro Sekunde** zwischen 1 und 100 mit dem einrastenden Regler oder im Zahlenfeld einstellen. Pfeiltasten ändern den Wert in Einzelschritten.
3. Maus zum gewünschten Ziel bewegen und **F6** drücken. Noch einmal **F6** stoppt.
4. Für einen eigenen Hotkey auf die Belegung neben **Hotkey** klicken und eine Taste oder Kombination drücken, etwa **Strg + Alt + K**. **Esc** bricht die Änderung ab.

Die Schaltfläche **Starten** lässt eine Sekunde Zeit zum Positionieren der Maus. **Stoppen** oder der Hotkey brechen auch diesen Vorlauf ab. Der globale Hotkey funktioniert bei minimiertem Fenster. Bereits belegte oder reservierte Hotkeys werden abgewiesen; die bisherige Belegung bleibt erhalten. Falls der gespeicherte Hotkey beim Start belegt ist, zuerst einen anderen auswählen.

Automatische Klicks über dem eigenen App-Fenster werden übersprungen. Nach Verlassen des Fensters wird weitergeklickt. Einstellungen sind während des Betriebs gesperrt. Schließen, Windows-Sperre und Standby stoppen das Klicken. Die App startet immer im Zustand **Bereit**; Einstellungen werden automatisch gespeichert.

Windows kann simulierte Eingaben in Programme mit höheren Rechten blockieren. Manche Anwendungen verarbeiten künstliche Klicks nicht. Die tatsächliche Klickrate kann unter hoher Systemlast abweichen.

## Dateien

- Installierte App: `%LOCALAPPDATA%\Programs\Autoklicker\Autoklicker.exe`
- Einstellungen: `%LOCALAPPDATA%\Autoklicker\settings.json`
- Startverknüpfung: **Autoklicker** im tatsächlichen Windows-Desktopordner.

Die ausgelieferte Version enthält die .NET-Laufzeit. Es ist keine separate Laufzeitinstallation nötig. Zum Entfernen die App schließen, den genannten App-Ordner und die Desktop-Verknüpfung entfernen; bei Bedarf auch den Einstellungsordner.

## Entwicklung

Windows x64 und .NET-10-SDK. Oberfläche: WPF; globale Hotkeys: `RegisterHotKey`; Mausausgabe: `SendInput`; unterbrechbare Zeitsteuerung mit Windows-Wartetimer und monotoner Uhr. Es werden keine externen NuGet-Bibliotheken benötigt.

Das bei der Erstellung verwendete SDK liegt unter `%LOCALAPPDATA%\AutoklickerBuild\dotnet`. Die Buildskripte verwenden diesen Pfad, wenn vorhanden, ansonsten das SDK aus `PATH`.

Aus PowerShell im Projektordner:

```powershell
.\scripts\Build.ps1 -Test
.\scripts\Install.ps1 -Open
.\scripts\Build-Installer.ps1 -Test
```

Der NSIS-Installer liegt danach als `artifacts\Autoklicker-Setup-1.0.0-x64.exe` vor. Er installiert nur für das aktuelle Benutzerkonto, legt Desktop- und Startmenü-Verknüpfungen an und lässt `%LOCALAPPDATA%\Autoklicker\settings.json` bei einer Deinstallation bestehen.

Die Tests dauern wegen der vier 30-Sekunden-Messungen etwa zwei Minuten. Sie senden bei den Zeitmessungen keine realen Mausklicks. Das separate Testfenster für echte Klicks wird über `Autoklicker.Tests.exe --surface` geöffnet; es ist nicht Bestandteil der installierten App.

Die App vor dem vollständigen Testlauf schließen, damit die Pipe für die Instanzaktivierung frei ist. `--quick` führt nur die gezielten Optimierungs- und Zustandstests aus. `--profile <absolute JSON-Datei>` misst etwa vier Minuten lang Ruheverbrauch, minimiertes Fenster, Klickraten sowie 300 Start-/Stopp-Zyklen und 60 Pipe-Aktivierungen. Dabei werden Mausausgaben durch einen Zähler ersetzt und Testeinstellungen getrennt gespeichert. Der Messprozess enthält zusätzlich den Testcode; seine absoluten Speicherwerte sind deshalb separat von der installierten App zu bewerten. Der Vergleich verwendet denselben Testcode mit der jeweiligen Anwendungs-DLL. Diese Werkzeuge werden nicht mit der App ausgeliefert.

`--allocations` vergleicht zusätzlich den temporären verwalteten Speicher für Statuswechsel ohne Rendering und wiederholte identische Speicheraufrufe. Das ist ein isolierter Entwicklungstest, keine Messung des gesamten App-RAMs.

Das App-Symbol ist ein eigener geometrischer Entwurf. `scripts\New-Icon.ps1` erzeugt die ICO-Datei in mehreren Auflösungen. Quellcode liegt unter `src\Autoklicker`, Tests unter `tests\Autoklicker.Tests`. Der ursprüngliche Umfang steht in `PROJEKTPLAN.md`, die Abnahme in `PRUEFBERICHT.md`.
