using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Autoklicker.Services;

public sealed class ClickEngine(Func<bool> emitClick) : IDisposable
{
    private readonly object _lifecycle = new();
    private readonly object _sendGate = new();
    private Thread? _worker;
    private CancellationTokenSource? _stop;
    public bool IsRunning => _worker?.IsAlive == true;

    public bool Start(int clicksPerSecond, Action<Exception>? onError = null)
    {
        if (!SettingsStore.ValidRate(clicksPerSecond)) throw new ArgumentOutOfRangeException(nameof(clicksPerSecond));
        lock (_lifecycle)
        {
            if (IsRunning) return false;
            _stop?.Dispose();
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _worker = new Thread(() => Run(clicksPerSecond, token, onError))
            { IsBackground = true, Name = "Autoklicker – Klickausgabe" };
            _worker.Start();
            return true;
        }
    }

    private void Run(int rate, CancellationToken token, Action<Exception>? onError)
    {
        try
        {
            using var timer = new PrecisionTimer();
            WaitHandle[] handles = [token.WaitHandle, timer];
            double interval = (double)Stopwatch.Frequency / rate;
            double next = Stopwatch.GetTimestamp();
            while (!token.IsCancellationRequested)
            {
                double remaining = next - Stopwatch.GetTimestamp();
                if (remaining > 0)
                {
                    timer.Arm(remaining / Stopwatch.Frequency);
                    if (WaitHandle.WaitAny(handles) == 0) break;
                    continue;
                }
                lock (_sendGate)
                {
                    if (token.IsCancellationRequested) break;
                    if (!emitClick()) throw new InvalidOperationException("Windows konnte die Klicks nicht senden.");
                }
                next += interval;
                long now = Stopwatch.GetTimestamp();
                // Never catch up missed intervals with a burst of clicks.
                if (next < now) next = now + interval;
            }
        }
        catch (Exception ex) { onError?.Invoke(ex); }
    }

    public void Stop()
    {
        lock (_lifecycle)
        {
            lock (_sendGate) _stop?.Cancel();
            if (_worker is not null && _worker != Thread.CurrentThread) _worker.Join();
            _worker = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _stop?.Dispose();
    }

    private sealed class PrecisionTimer : WaitHandle
    {
        public PrecisionTimer()
        {
            SafeWaitHandle = Native.CreateWaitableTimerEx(0, null, 2, 0x001F0003);
            if (SafeWaitHandle.IsInvalid)
            {
                SafeWaitHandle.Dispose();
                SafeWaitHandle = Native.CreateWaitableTimerEx(0, null, 0, 0x001F0003);
            }
            if (SafeWaitHandle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Arm(double seconds)
        {
            long due = -Math.Max(1, (long)Math.Ceiling(seconds * 10_000_000));
            if (!Native.SetWaitableTimer(SafeWaitHandle, ref due, 0, 0, 0, false))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}

internal sealed class MouseOutput(nint window)
{
    private static readonly Native.Input[] Pair =
    [
        new() { Type = 0, Data = new() { Mouse = new() { Flags = 0x0002 } } },
        new() { Type = 0, Data = new() { Mouse = new() { Flags = 0x0004 } } }
    ];
    private static readonly Native.Input[] Release = [Pair[1]];

    public bool Emit()
    {
        if (!Native.GetCursorPos(out var point)) return false;
        if (!Native.IsIconic(window))
        {
            if (!Native.GetWindowRect(window, out var rect)) return false;
            if (point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom)
                return true;
        }
        uint sent = Native.SendInput(2, Pair, Marshal.SizeOf<Native.Input>());
        if (sent == 1) Native.SendInput(1, Release, Marshal.SizeOf<Native.Input>());
        return sent == 2;
    }
}
