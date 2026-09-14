using System.Runtime.InteropServices;

namespace Autoklicker.Services;

internal sealed class TrayIcon : IDisposable
{
    internal const int CallbackMessage = 0x8001;
    private const uint IconId = 1;
    private const uint NotifyAdd = 0;
    private const uint NotifyDelete = 2;
    private const uint NotifyMessage = 1;
    private const uint NotifyIconFlag = 2;
    private const uint NotifyTip = 4;
    private const int LeftButtonUp = 0x0202;
    private const int LeftButtonDoubleClick = 0x0203;
    private const int RightButtonUp = 0x0205;
    private const int KeyboardSelect = 0x0401;
    private readonly nint _window;
    private readonly uint _taskbarCreatedMessage;
    private readonly bool _ownsIcon;
    private nint _icon;
    private bool _visible;
    private bool _disposed;

    internal TrayIcon(nint window)
    {
        _window = window;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _icon = LoadProcessIcon(out _ownsIcon);
    }

    internal bool Show()
    {
        if (_disposed || _icon == 0) return false;
        if (_visible) return true;
        _visible = Add();
        return _visible;
    }

    internal void Hide()
    {
        if (!_visible) return;
        var data = CreateData();
        ShellNotifyIcon(NotifyDelete, ref data);
        _visible = false;
    }

    internal bool ProcessMessage(int message, nint lParam)
    {
        if (_taskbarCreatedMessage != 0 && unchecked((uint)message) == _taskbarCreatedMessage)
        {
            if (_visible) Add();
            return false;
        }

        if (message != CallbackMessage) return false;
        int notification = unchecked((int)(long)lParam) & 0xFFFF;
        return notification is LeftButtonUp or LeftButtonDoubleClick or RightButtonUp or KeyboardSelect;
    }

    private bool Add()
    {
        var data = CreateData();
        return ShellNotifyIcon(NotifyAdd, ref data);
    }

    private NotifyIconData CreateData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = _window,
        Id = IconId,
        Flags = NotifyMessage | NotifyIconFlag | NotifyTip,
        CallbackMessage = CallbackMessage,
        Icon = _icon,
        Tip = "Autoklicker – zum Öffnen anklicken",
        Info = string.Empty,
        InfoTitle = string.Empty
    };

    private static nint LoadProcessIcon(out bool ownsIcon)
    {
        ownsIcon = false;
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && ExtractIconEx(path, 0, out nint large, out nint small, 1) > 0)
        {
            nint selected = small != 0 ? small : large;
            if (large != 0 && large != selected) DestroyIcon(large);
            if (small != 0 && small != selected) DestroyIcon(small);
            ownsIcon = selected != 0;
            if (selected != 0) return selected;
        }

        // Shared Windows application icon; it must not be destroyed by this process.
        return LoadIcon(0, (nint)32512);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        if (_ownsIcon && _icon != 0) DestroyIcon(_icon);
        _icon = 0;
        _disposed = true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        internal uint Size;
        internal nint Window;
        internal uint Id;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Tip;
        internal uint State;
        internal uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Info;
        internal uint VersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string InfoTitle;
        internal uint InfoFlags;
        internal Guid ItemGuid;
        internal nint BalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
}
