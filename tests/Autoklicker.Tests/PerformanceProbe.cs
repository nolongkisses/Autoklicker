using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Autoklicker;
using Autoklicker.Services;

// Development-only measurements: real window/worker/timer, counting callback instead of mouse injection.
internal static class PerformanceProbe
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<object> Results = [];
    private static object Sample(string label)
    {
        using var p = Process.GetCurrentProcess();
        return new { label, seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
            cpu = p.TotalProcessorTime.TotalSeconds, workingMiB = p.WorkingSet64 / 1048576.0,
            privateMiB = p.PrivateMemorySize64 / 1048576.0, handles = p.HandleCount,
            threads = p.Threads.Count, allocatedBytes = GC.GetTotalAllocatedBytes() };
    }
    private static void Mark(string label) { var sample = Sample(label); Results.Add(sample); Console.WriteLine(JsonSerializer.Serialize(sample)); }
    private static void Invoke(MainWindow window, string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, arguments);

    internal static int Run(string output)
    {
        var app = new TestApp();
        app.InitializeTheme();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        Task task = Measure(app);
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => app.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        File.WriteAllText(output, JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        try { task.GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    private static async Task Measure(App app)
    {
        var window = new MainWindow();
        string directory = Path.Combine(Path.GetTempPath(), "AutoklickerTests", Guid.NewGuid().ToString("N"));
        typeof(MainWindow).GetField("_store", Private)!.SetValue(window, new SettingsStore(directory));
        typeof(MainWindow).GetField("_settings", Private)!.SetValue(window, new Settings(10, 0x87, 3));
        app.MainWindow = window;
        window.Show();
        var original = (ClickEngine)typeof(MainWindow).GetField("_engine", Private)!.GetValue(window)!;
        original.Dispose();
        int clicks = 0;
        var engine = new ClickEngine(() => { Interlocked.Increment(ref clicks); return true; });
        typeof(MainWindow).GetField("_engine", Private)!.SetValue(window, engine);
        using var stop = new CancellationTokenSource();
        Task listener = (Task)typeof(App).GetMethod("ListenForActivation", Private)!.Invoke(app, [stop.Token])!;
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        string pipeName = "Autoklicker.8B18A3D2" + identity.User!.Value;
        try
        {
            await Task.Delay(5000);
            Mark("idle-start"); await Task.Delay(60000); Mark("idle-end");
            window.WindowState = WindowState.Minimized;
            await Task.Delay(2000);
            Mark("minimized-start"); await Task.Delay(30000); Mark("minimized-end");
            window.WindowState = WindowState.Normal;
            foreach (int rate in new[] { 1, 10, 50, 100 })
            {
                Invoke(window, "SetRate", rate, true);
                await Task.Delay(500);
                clicks = 0;
                Mark($"rate-{rate}-start");
                var watch = Stopwatch.StartNew();
                Invoke(window, "Toggle", false);
                await Task.Delay(30000);
                var stopping = Stopwatch.StartNew();
                Invoke(window, "StopClicking");
                double stopMs = stopping.Elapsed.TotalMilliseconds;
                double seconds = watch.Elapsed.TotalSeconds;
                int count = clicks;
                Mark($"rate-{rate}-end");
                var result = new { rate, count, seconds, stopMs };
                Results.Add(result); Console.WriteLine(JsonSerializer.Serialize(result));
                await Task.Delay(150);
                if (clicks != count || Math.Abs(count / seconds / rate - 1) > .05 || stopMs >= 100)
                    throw new Exception("Timing-/Stoppprüfung fehlgeschlagen");
            }
            for (int block = 1; block <= 3; block++)
            {
                Mark($"stress-{block}-start");
                for (int i = 0; i < 100; i++)
                {
                    Invoke(window, "Toggle", false);
                    await Task.Delay(1);
                    Invoke(window, "StopClicking");
                    await Task.Delay(1);
                }
                for (int i = 0; i < 20; i++)
                {
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                    await client.ConnectAsync(2000);
                    await client.WriteAsync(new byte[] { 1 });
                    await Task.Delay(30);
                }
                await Task.Delay(5000);
                Mark($"stress-{block}-end");
            }
            await Task.Delay(10000); Mark("final-idle");
        }
        finally
        {
            stop.Cancel();
            await listener;
            window.Close();
        }
    }
}
