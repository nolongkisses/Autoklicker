using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autoklicker;
using Autoklicker.Services;

internal static class Program
{
    private static int _failed;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--surface")) { RunSurface(); return 0; }
        if (args.Contains("--render")) { Render(args.Last()); return 0; }
        if (args.Contains("--profile")) return PerformanceProbe.Run(args.Last());
        if (args.Contains("--allocations")) return AllocationProbe.Run();
        OptimizationTests.Run(Test);
        if (args.Contains("--quick")) return _failed == 0 ? 0 : 1;
        Test("Validierung von Klickrate und reservierten Hotkeys", () =>
        {
            foreach (int value in new[] { 1, 10, 50, 100 }) Assert(SettingsStore.ValidRate(value), "Gültiger Wert abgelehnt");
            foreach (int value in new[] { -1, 0, 101, int.MaxValue }) Assert(!SettingsStore.ValidRate(value), "Ungültiger Wert akzeptiert");
            Assert(HotkeyService.IsAllowed(0x75, 0), "F6");
            Assert(HotkeyService.IsAllowed(0x4B, 3), "Strg+Alt+K");
            foreach (var pair in new (uint, uint)[] { (0x7B, 0), (0x41, 8), (0x10, 0), (0x73, 1), (0x2E, 3), (0, 0) })
                Assert(!HotkeyService.IsAllowed(pair.Item1, pair.Item2), "Reservierte Taste akzeptiert");
        });
        Test("Atomare Speicherung, defekte Datei und Schreibfehler", () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "AutoklickerTests", Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(directory);
            Assert(store.Load(out _) == new Settings(), "Standardwerte");
            var expected = new Settings(50, 0x4B, 3);
            Assert(store.Save(expected), "Speichern");
            Assert(store.Load(out var warning) == expected && warning is null, "Laden");
            File.WriteAllText(Path.Combine(directory, "settings.json"), "defekt");
            Assert(store.Load(out warning) == new Settings() && warning is not null, "Defekte Datei");
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"ClicksPerSecond\":500}");
            Assert(store.Load(out warning) == new Settings() && warning is not null, "Ungültige gespeicherte Rate");
            Assert(!store.Save(new Settings(0)), "Ungültige Speicherung");
            string blocked = Path.Combine(directory, "blocked");
            File.WriteAllText(blocked, "Datei anstelle Verzeichnis");
            Assert(!new SettingsStore(blocked).Save(expected), "Schreibfehler muss kontrolliert bleiben");
        });
        Test("Echte Windows-Hotkey-Konflikte und Freigabe", () =>
        {
            using var source = new HwndSource(new HwndSourceParameters("Autoklicker Hotkey Test") { Width = 1, Height = 1, WindowStyle = 0 });
            using var first = new HotkeyService(source.Handle);
            using var secondSource = new HwndSource(new HwndSourceParameters("Autoklicker Hotkey Test 2") { Width = 1, Height = 1, WindowStyle = 0 });
            using var second = new HotkeyService(secondSource.Handle);
            Assert(first.TrySet(0x86, 3), "Test-Hotkey Strg+Alt+F23 nicht verfügbar");
            Assert(second.TrySet(0x85, 3), "Test-Hotkey Strg+Alt+F22 nicht verfügbar");
            Assert(!first.TrySet(0x85, 3), "Konflikt nicht erkannt");
            Assert(first.IsRegistered && first.Key == 0x86, "Alte Belegung verloren");
            Assert(!second.TrySet(0x86, 3), "Alte Belegung nicht mehr reserviert");
            first.Dispose();
            Assert(second.TrySet(0x86, 3), "Hotkey nicht freigegeben");
        });
        Test("INPUT-Struktur entspricht Windows-x64-ABI", () => Assert(Marshal.SizeOf<Native.Input>() == 40, "Falsche INPUT-Größe"));
        Test("Schneller Stopp bei 1 Klick/s und keine Ausgabe nach Stopp", () =>
        {
            int count = 0;
            using var firstClick = new ManualResetEventSlim();
            using var engine = new ClickEngine(() => { Interlocked.Increment(ref count); firstClick.Set(); return true; });
            Assert(engine.Start(1), "Start fehlgeschlagen");
            Assert(firstClick.Wait(2000), "Erster Klick fehlt");
            Assert(!engine.Start(100), "Doppelstart akzeptiert");
            var timer = Stopwatch.StartNew();
            engine.Stop();
            Console.WriteLine($"  Stopp: {timer.Elapsed.TotalMilliseconds:F2} ms");
            Assert(timer.ElapsedMilliseconds < 100, "Stopp zu langsam");
            int stopped = count;
            Thread.Sleep(150);
            Assert(count == stopped && !engine.IsRunning, "Ausgabe nach Stopp");
            for (int i = 0; i < 30; ++i) { engine.Start(100); engine.Stop(); }
            Assert(!engine.IsRunning, "Worker nach schnellem Umschalten aktiv");
        });
        Test("Fehlgeschlagene Klickausgabe beendet Worker", () =>
        {
            using var failed = new ManualResetEventSlim();
            using var engine = new ClickEngine(() => false);
            engine.Start(10, _ => failed.Set());
            Assert(failed.Wait(2000), "Fehler nicht gemeldet");
            engine.Stop();
            Assert(!engine.IsRunning, "Worker läuft weiter");
        });
        foreach (int rate in new[] { 1, 10, 50, 100 })
        {
            Test($"Klickrate {rate}/s über 30 Sekunden", () =>
            {
                int count = 0;
                Exception? error = null;
                using var engine = new ClickEngine(() => { Interlocked.Increment(ref count); return true; });
                var watch = Stopwatch.StartNew();
                engine.Start(rate, ex => error = ex);
                Thread.Sleep(30_000);
                engine.Stop();
                double seconds = watch.Elapsed.TotalSeconds;
                double measured = count / seconds;
                double deviation = Math.Abs(measured / rate - 1);
                Console.WriteLine($"  {count} Ausgaben / {seconds:F3} s = {measured:F3}/s; Abweichung {deviation:P2}");
                Assert(error is null && deviation <= .05, "Klickrate außerhalb ±5 %");
            });
        }
        Console.WriteLine(_failed == 0 ? "ALLE TESTS BESTANDEN" : $"{_failed} TESTS FEHLGESCHLAGEN");
        return _failed == 0 ? 0 : 1;
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { _failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
        Console.Out.Flush();
    }

    private static void RunSurface()
    {
        var app = new Application();
        int down = 0, up = 0;
        var count = new TextBlock { Text = "Drücken: 0   Loslassen: 0", FontSize = 24, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var area = new Grid { Background = new SolidColorBrush(Color.FromRgb(30, 35, 40)) };
        area.Children.Add(count);
        var hint = new TextBlock { Text = "Lokale Testfläche · zählt ausschließlich Klicks\nF6 startet/stoppt den Autoklicker", Foreground = Brushes.LightGray, Margin = new Thickness(20), FontSize = 14, IsHitTestVisible = false };
        area.Children.Add(hint);
        area.MouseDown += (_, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) down++; count.Text = $"Drücken: {down}   Loslassen: {up}"; };
        area.MouseUp += (_, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) up++; count.Text = $"Drücken: {down}   Loslassen: {up}"; };
        var window = new Window { Title = "Autoklicker – lokale Testfläche", Width = 580, Height = 340, Left = 30, Top = 70, Content = area };
        app.Run(window);
    }

    private static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(340, 300));
        root.Arrange(new Rect(0, 0, 340, 300));
        root.UpdateLayout();
        foreach (double scale in new[] { 1.0, 1.5, 2.0 })
        {
            var image = new RenderTargetBitmap((int)(340 * scale), (int)(300 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(Path.Combine(directory, $"Autoklicker-{scale * 100:0}.png"));
            encoder.Save(stream);
            Console.WriteLine($"Gerendert: {scale:P0}");
        }
    }
}
