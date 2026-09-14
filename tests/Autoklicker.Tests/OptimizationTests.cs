using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Autoklicker;
using Autoklicker.Services;

internal static class OptimizationTests
{
    private static readonly DateTime Sentinel = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "AutoklickerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    internal static void Run(Action<string, Action> test)
    {
        test("Unveränderte Einstellungen schreiben die Datei nicht erneut", () =>
        {
            string directory = NewDirectory(), path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(directory);
            var settings = store.Load(out _);
            Assert(store.Save(settings) && File.Exists(path), "Erster Lauf muss Datei anlegen");
            File.SetLastWriteTimeUtc(path, Sentinel);
            Assert(store.Save(settings with { }) && File.GetLastWriteTimeUtc(path) == Sentinel, "Identische Werte erneut geschrieben");
            var reloaded = new SettingsStore(directory);
            Assert(reloaded.Load(out _) == settings && reloaded.Save(settings), "Neustart/Laden");
            Assert(File.GetLastWriteTimeUtc(path) == Sentinel, "Geladene Werte erneut geschrieben");
        });
        test("Fehlgeschlagene Speicherung bleibt wiederholbar; Rückkehr zu gespeichertem Wert", () =>
        {
            string directory = NewDirectory(), path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(directory);
            var old = new Settings(10);
            var next = new Settings(50);
            Assert(store.Save(old), "Vorbereitung");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert(!store.Save(next), "Gesperrte Datei muss Speichern verhindern");
            Assert(store.Save(next), "Wiederholung nach Fehler fehlgeschlagen");
            Assert(new SettingsStore(directory).Load(out _) == next, "Fehlgeschlagene Werte fälschlich als gespeichert behandelt");
            Assert(store.Save(old) && new SettingsStore(directory).Load(out _) == old, "Rückkehr zum alten Wert nicht gespeichert");
            Assert(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Temporäre Datei zurückgeblieben");
        });
        test("Defekte oder fehlende Datei wird auch mit Standardwerten repariert", () =>
        {
            string directory = NewDirectory(), path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(directory);
            Assert(store.Save(new Settings()), "Vorbereitung");
            File.WriteAllText(path, "defekt");
            var defaults = store.Load(out var warning);
            Assert(warning is not null && store.Save(defaults), "Defekte Datei nicht repariert");
            Assert(new SettingsStore(directory).Load(out warning) == defaults && warning is null, "Reparatur ungültig");
            File.Delete(path);
            Assert(store.Save(store.Load(out _)) && File.Exists(path), "Fehlende Datei nicht neu angelegt");
        });

        var app = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeTheme();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        test("Fensterbuttons zeigen keinen Tastatur-Fokusrahmen", () =>
        {
            var window = new MainWindow(new SettingsStore(NewDirectory()));
            try
            {
                var minimize = (Button)window.FindName("MinimizeButton");
                var close = (Button)window.FindName("CloseButton");
                Assert(!minimize.Focusable && !minimize.IsTabStop, "Minimieren kann Fokusrahmen erhalten");
                Assert(!close.Focusable && !close.IsTabStop, "Schließen kann Fokusrahmen erhalten");
            }
            finally { window.Close(); }
        });
        test("Minimieren wechselt in den Tray und Tray-Klick stellt das Fenster wieder her", () =>
        {
            var window = new MainWindow(new SettingsStore(NewDirectory()));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            window.Show();
            try
            {
                typeof(MainWindow).GetMethod("MinimizeToTray", flags)!.Invoke(window, null);
                PumpFor(50);
                Assert(!window.IsVisible && !window.ShowInTaskbar && window.WindowState == WindowState.Minimized,
                    "Fenster bleibt nach Tray-Minimierung sichtbar oder in der Taskleiste");

                object[] message = [(nint)0, TrayIcon.CallbackMessage, (nint)0, (nint)0x0202, false];
                typeof(MainWindow).GetMethod("WindowMessage", flags)!.Invoke(window, message);
                PumpFor(50);
                Assert((bool)message[4], "Tray-Klick wurde nicht verarbeitet");
                Assert(window.IsVisible && window.ShowInTaskbar && window.WindowState == WindowState.Normal,
                    "Tray-Klick stellt das Fenster nicht vollständig wieder her");
            }
            finally { window.Close(); }
        });
        test("Schnelle Änderungen, Normalisierung und Speichern direkt beim Schließen", () =>
        {
            string directory = NewDirectory(), path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(directory);
            Assert(store.Save(new Settings()), "Vorbereitung");
            var window = new MainWindow(store);
            new WindowInteropHelper(window).EnsureHandle();
            try
            {
                var box = (TextBox)window.FindName("RateBox");
                box.Text = "20"; box.Text = "30"; box.Text = "40";
                PumpFor(550);
                Assert(new SettingsStore(directory).Load(out _) == new Settings(40), "Letzte schnelle Änderung fehlt");
                File.SetLastWriteTimeUtc(path, Sentinel);
                box.Text = "040";
                typeof(MainWindow).GetMethod("CommitRate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                PumpFor(550);
                Assert(box.Text == "40", "Unveränderte Zahl nicht normalisiert");
                Assert(File.GetLastWriteTimeUtc(path) == Sentinel, "Normalisierung löst Schreibvorgang aus");
                box.Text = "75";
            }
            finally { window.Close(); }
            Assert(new SettingsStore(directory).Load(out _) == new Settings(75), "Letzte Änderung beim Schließen verloren");
        });
        test("Pipe-Abbruch beendet auch einen Client ohne Nutzdaten", () =>
        {
            using var stop = new CancellationTokenSource();
            Task listener = (Task)typeof(App).GetMethod("ListenForActivation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [stop.Token])!;
            string suffix = (string)typeof(App).GetField("UserSuffix", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            using var client = new NamedPipeClientStream(".", "Autoklicker.8B18A3D2" + suffix, PipeDirection.Out);
            try { client.Connect(2000); }
            finally { stop.Cancel(); }
            Assert(listener.Wait(2000) && listener.IsCompletedSuccessfully, "Pipe-Abbruch hängt oder wirft Fehler");
        });
        test("Pipe-Abbruch wartet nicht auf eine blockierte UI-Aktivierung", () =>
        {
            var window = new MainWindow(new SettingsStore(NewDirectory()));
            app.MainWindow = window;
            new WindowInteropHelper(window).EnsureHandle();
            using var stop = new CancellationTokenSource();
            Task listener = (Task)typeof(App).GetMethod("ListenForActivation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [stop.Token])!;
            string suffix = (string)typeof(App).GetField("UserSuffix", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            try
            {
                using var client = new NamedPipeClientStream(".", "Autoklicker.8B18A3D2" + suffix, PipeDirection.Out);
                client.Connect(2000);
                client.WriteByte(1);
                // Intentionally do not pump the UI: shutdown must cancel a queued activation.
                Thread.Sleep(100);
                stop.Cancel();
                Assert(listener.Wait(2000) && listener.IsCompletedSuccessfully, "Abbruch hängt von der UI ab");
            }
            finally { stop.Cancel(); window.Close(); }
        });
        test("Sofortiges Speichern erledigt eine bereits vorgemerkte Änderung", () =>
        {
            string directory = NewDirectory(), path = Path.Combine(directory, "settings.json");
            var window = new MainWindow(new SettingsStore(directory));
            new WindowInteropHelper(window).EnsureHandle();
            try
            {
                ((TextBox)window.FindName("RateBox")).Text = "60";
                typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                var delay = (DispatcherTimer)typeof(MainWindow).GetField("_saveDelay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                Assert(!delay.IsEnabled, "Erledigter Speichertimer läuft weiter");
                File.SetLastWriteTimeUtc(path, Sentinel);
                PumpFor(550);
                Assert(File.GetLastWriteTimeUtc(path) == Sentinel, "Erledigte Änderung nochmals gespeichert");
            }
            finally { window.Close(); }
        });
        test("Vorlaufabbruch und Sitzungs-/Energiesignale stoppen die Ausgabe", () =>
        {
            var store = new SettingsStore(NewDirectory());
            Assert(store.Save(new Settings(100, 0x84, 3)), "Testbelegung vorbereiten");
            var window = new MainWindow(store);
            new WindowInteropHelper(window).EnsureHandle();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ((ClickEngine)typeof(MainWindow).GetField("_engine", flags)!.GetValue(window)!).Dispose();
            int count = 0;
            var engine = new ClickEngine(() => { Interlocked.Increment(ref count); return true; });
            typeof(MainWindow).GetField("_engine", flags)!.SetValue(window, engine);
            try
            {
                typeof(MainWindow).GetMethod("Toggle", flags)!.Invoke(window, [true]);
                typeof(MainWindow).GetMethod("Toggle", flags)!.Invoke(window, [false]);
                PumpFor(1100);
                Assert(count == 0 && !engine.IsRunning, "Abgebrochener Vorlauf klickt später");
                foreach (var (message, signal) in new[] { (0x02B1, 7), (0x02B1, 2), (0x0218, 4), (0x0218, 18) })
                {
                    typeof(MainWindow).GetMethod("Toggle", flags)!.Invoke(window, [false]);
                    Assert(engine.IsRunning, "Vorbereitung: Worker startet nicht");
                    object[] args = [(nint)0, message, (nint)signal, (nint)0, false];
                    typeof(MainWindow).GetMethod("WindowMessage", flags)!.Invoke(window, args);
                    int stopped = count;
                    PumpFor(30);
                    Assert(!engine.IsRunning && count == stopped, "Sitzungs-/Energiesignal stoppt nicht dauerhaft");
                }
            }
            finally { window.Close(); }
        });
    }

    private static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
