# Autoklicker – Liefer- und Prüfbericht

Stand: 13. September 2026. Version 1.0.0.

Nachfolgende Wartung mit Ressourcen-, Speicher- und UI-Allokationsoptimierung: siehe `OPTIMIERUNGSBERICHT.md` für die aktualisierten Messwerte, 18 bestandene Tests und erneute Windows-Prüfung. Die folgenden Abschnitte dokumentieren die ursprüngliche Lieferung.

## Lieferung

Die App wurde gemäß Projektplan als C#-/WPF-Anwendung für Windows x64 umgesetzt. Das schwarze Fenster misst 340 × 300 logische Pixel. Enthalten sind der Regler mit 1–100 Raststufen, synchrones Zahlenfeld, frei belegbarer globaler Start-/Stopp-Hotkey, Vorlauf beim Schaltflächenstart, lokales Speichern und Schutz vor mehreren Instanzen.

- Installiert unter `C:\Users\Anwender\AppData\Local\Programs\Autoklicker`.
- Desktop-Verknüpfung: `C:\Users\Anwender\Desktop\Autoklicker.lnk`.
- Eigenständiges Release mit enthaltener Laufzeit: `artifacts\release`.
- Quellprojekt, Build-/Installationsskripte und deutsche Anleitung liegen im Projektordner.
- Eigenes mehrstufiges ICO-Symbol, erzeugt aus geometrischen Formen.
- Für die Entwicklung wurde das offizielle .NET SDK 10.0.401 lokal bereitgestellt.

## Automatisierte Prüfungen

Release-Build: **0 Fehler, 0 Warnungen**. Der Testlauf unter `tests\Autoklicker.Tests` war vollständig erfolgreich.

| Prüfung | Ergebnis |
| --- | --- |
| Gültige und ungültige Klickraten | Bestanden |
| Reservierte Hotkeys, F12 und Windows-Kombinationen | Bestanden |
| Speichern/Laden, beschädigte Einstellungen und Schreibfehler | Bestanden |
| Echte Windows-Hotkey-Registrierung, Konflikt, Beibehaltung der alten Taste und Freigabe | Bestanden |
| Korrekte Größe der Windows-INPUT-Struktur für x64 | Bestanden |
| Zweiter Start bei laufendem Worker | Verhindert |
| Stoppen bei 1 Klick/s | 0,51 ms im Test |
| Keine weiteren Ausgaben nach Stopp | Bestanden |
| 30 schnelle Start-/Stopp-Zyklen | Kein verbleibender Worker |
| Fehlgeschlagene Ausgabe | Worker beendet, Fehler gemeldet |

### Zeitmessungen

Je 30 Sekunden mit dem produktiven Klickdienst und einer zählenden Testausgabe. Diese Messungen erfassen die Zeitsteuerung; es wurden dafür keine Mausklicks auf fremde Anwendungen gesendet. Die erste Ausgabe erfolgt sofort, weshalb eine zusätzliche Ausgabe am Rand des Messfensters möglich ist.

| Soll | Ausgaben | Dauer | Gemessen | Abweichung |
| --- | ---: | ---: | ---: | ---: |
| 1/s | 31 | 30,006 s | 1,033/s | 3,31 % |
| 10/s | 301 | 30,008 s | 10,031/s | 0,31 % |
| 50/s | 1.501 | 30,006 s | 50,023/s | 0,05 % |
| 100/s | 3.001 | 30,007 s | 100,011/s | 0,01 % |

Alle Messungen liegen innerhalb der im Plan vorgesehenen ±5 %.

## Praktische Windows-Prüfung

- App-Fenster mit schwarzer Oberfläche visuell geprüft.
- Zahleneingabe 50 verändert die Reglerstellung korrekt; Rückstellung auf 10 geprüft.
- Aufnahme von **Strg + Alt + K** erfolgreich; Abbruch einer weiteren Aufnahme mit **Esc** erhält die bisherige Belegung.
- Eigener Hotkey startet und stoppt bei fokussierter anderer Anwendung.
- Auf einer lokalen WPF-Testfläche wurden in einem Durchlauf **355 automatische Klickpaare** gezählt. Drücken und Loslassen stimmen überein; nach Stopp blieb der Zähler unverändert.
- Bei minimiertem Autoklicker wurden in einem weiteren Durchlauf **267 automatische Klickpaare** gezählt. Start und Stopp erfolgten über denselben globalen Hotkey.
- Gespeicherte Einstellungen wurden von der installierten App nach erneutem Start übernommen; der Zustand war gestoppt.
- Die Desktop-Verknüpfung startet die installierte App. Ein zweiter Start beendet die neue Instanz mit Exitcode 0; die vorhandene Instanz bleibt bestehen.
- EXE und Anwendungs-DLL der Installation stimmen per SHA-256-Prüfung mit dem Release überein.
- Testwerte wurden zur Übergabe auf **10 Klicks/s und F6** zurückgestellt.

## Darstellung und verbleibende Prüfgrenzen

WPF-Renderings bei 100 %, 150 % und 200 % wurden erzeugt; Texte und Bedienelemente passen in die vorgesehene Fläche. Die Prüfung der Renderings ersetzt keinen physischen Wechsel zwischen Monitoren mit unterschiedlicher DPI.

Das Stoppen bei tatsächlicher Windows-Sperre, Standby und Sitzungsabmeldung wurde implementiert, aber auf diesem laufenden Arbeitsplatz nicht ausgelöst. Auch Gedrückthalten des Hotkeys, Vorlaufabbruch innerhalb der einen Sekunde, Selbstklickschutz und Schließen während laufender Ausgabe wurden nicht separat als vollständige UI-Abläufe automatisiert. Die Umsetzung verwendet `MOD_NOREPEAT`, unterbrechbare Wartezeiten, Fenstergrenzenprüfung und synchrones Beenden des Workers; die entsprechenden Stellen wurden im Code geprüft.

Windows ist kein Echtzeitsystem. Die Messwerte sind keine Garantie unter hoher Last oder in Anwendungen, die synthetische Eingaben filtern. Eingaben in höher privilegierte Programme können von Windows blockiert werden.
