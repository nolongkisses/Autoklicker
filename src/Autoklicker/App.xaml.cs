using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace Autoklicker;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private CancellationTokenSource? _pipeStop;
    private Task? _pipeTask;
    private const string InstanceName = "Autoklicker.8B18A3D2";
    private static readonly string UserSuffix = GetUserSuffix();

    private static string GetUserSuffix()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return identity.User!.Value;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(true, @"Local\" + InstanceName + UserSuffix, out bool first);
        if (!first)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", InstanceName + UserSuffix, PipeDirection.Out);
                pipe.Connect(2500);
                pipe.WriteByte(1);
            }
            catch (IOException) { }
            catch (TimeoutException) { }
            Shutdown();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
        _pipeStop = new CancellationTokenSource();
        _pipeTask = ListenForActivation(_pipeStop.Token);
    }

    private async Task ListenForActivation(CancellationToken token)
    {
        try
        {
            byte[] data = new byte[1];
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(InstanceName + UserSuffix,
                        PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    if (await pipe.ReadAsync(data, token).ConfigureAwait(false) == 1 && data[0] == 1)
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (MainWindow.WindowState == WindowState.Minimized)
                                MainWindow.WindowState = WindowState.Normal;
                            MainWindow.Show();
                            MainWindow.Activate();
                            MainWindow.Topmost = true;
                            MainWindow.Topmost = false;
                        }, System.Windows.Threading.DispatcherPriority.Normal, token).Task.ConfigureAwait(false);
                }
                catch (IOException) { await Task.Delay(100, token).ConfigureAwait(false); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeStop?.Cancel();
        if (_pipeTask is not null)
            _ = FinishPipeShutdown(_pipeTask, _pipeStop!);
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static async Task FinishPipeShutdown(Task task, CancellationTokenSource stop)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
        finally { stop.Dispose(); }
    }
}
