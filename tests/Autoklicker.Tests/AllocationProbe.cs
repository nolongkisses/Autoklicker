using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autoklicker;
using Autoklicker.Services;

internal static class AllocationProbe
{
    internal static int Run()
    {
        var app = new TestApp();
        app.InitializeTheme();
        var window = new MainWindow();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(MainWindow).GetMethod("UpdateControls", flags)!;
        var state = typeof(MainWindow).GetField("_state", flags)!;
        object idle = Enum.ToObject(state.FieldType, 0), running = Enum.ToObject(state.FieldType, 2);
        void ChangeState()
        {
            state.SetValue(window, running); update.Invoke(window, null);
            state.SetValue(window, idle); update.Invoke(window, null);
        }
        for (int i = 0; i < 10; i++) ChangeState();
        Measure("1000 UI-Statuspaare ohne Rendering", 1000, ChangeState);
        string directory = Path.Combine(Path.GetTempPath(), "AutoklickerTests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory);
        var settings = new Settings();
        if (!store.Save(settings)) throw new IOException("Vorbereitung fehlgeschlagen");
        Measure("100 identische Speicheraufrufe", 100, () =>
        {
            if (!store.Save(settings)) throw new IOException("Speichern fehlgeschlagen");
        });
        return 0;
    }

    private static void Measure(string name, int iterations, Action action)
    {
        var timer = Stopwatch.StartNew();
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        timer.Stop();
        Console.WriteLine(JsonSerializer.Serialize(new { name, iterations, allocatedBytes = allocated, milliseconds = timer.Elapsed.TotalMilliseconds }));
    }
}
