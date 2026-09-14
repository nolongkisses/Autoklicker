using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Autoklicker.Services;

namespace Autoklicker;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush Light = Gray(0xF1);
    private static readonly SolidColorBrush Dark = Gray(0x0D);
    private static readonly SolidColorBrush ActiveBackground = Gray(0x23);
    private static readonly SolidColorBrush ActiveBorder = Gray(0x50);
    private static readonly SolidColorBrush IdleDot = Gray(0x71);
    private static readonly SolidColorBrush ActiveDot = Gray(0xE0);
    private static readonly SolidColorBrush MutedText = Gray(0x94);
    private static readonly SolidColorBrush WarningText = Gray(0xD4);
    private static readonly Geometry StartIcon = Frozen(Geometry.Parse("M 0,0 L 0,10 L 8,5 Z"));
    private static readonly Geometry StopIcon = Frozen(Geometry.Parse("M 0,0 L 9,0 L 9,9 L 0,9 Z"));
    private readonly SettingsStore _store;
    private Settings _settings;
    private HotkeyService? _hotkey;
    private ClickEngine? _engine;
    private HwndSource? _source;
    private nint _handle;
    private bool _ready, _syncing, _capturing, _closing, _sessionNotifications;
    private long _generation;
    private RunState _state;
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _saveDelay = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private enum RunState { Idle, Pending, Running }

    public MainWindow() : this(new SettingsStore()) { }

    internal MainWindow(SettingsStore store)
    {
        _store = store;
        _settings = _store.Load(out string? warning);
        InitializeComponent();
        _countdown.Tick += (_, _) => { _countdown.Stop(); BeginClicking(); };
        _saveDelay.Tick += (_, _) => { _saveDelay.Stop(); SaveSettings(); };
        RateSlider.Value = _settings.ClicksPerSecond;
        RateBox.Text = _settings.ClicksPerSecond.ToString(CultureInfo.InvariantCulture);
        _ready = true;
        SourceInitialized += (_, _) => InitializeNative(warning);
        ContentRendered += (_, _) => WindowFrame.Apply(this);
        StateChanged += (_, _) => Dispatcher.BeginInvoke(() => WindowFrame.Apply(this));
        Deactivated += (_, _) => { if (_capturing) EndCapture("Hotkey unverändert."); };
    }

    private void InitializeNative(string? warning)
    {
        _handle = new WindowInteropHelper(this).Handle;
        WindowFrame.Apply(this);
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowMessage);
        _hotkey = new HotkeyService(_handle);
        _engine = new ClickEngine(new MouseOutput(_handle).Emit);
        _sessionNotifications = Native.WTSRegisterSessionNotification(_handle, 0);
        bool registered = _hotkey.TrySet(_settings.Hotkey, _settings.Modifiers);
        UpdateControls();
        if (!registered) SetStatus("Hotkey belegt. Bitte einen anderen wählen.", true);
        else if (!_sessionNotifications) SetStatus("Sitzungsüberwachung nicht verfügbar.", true);
        else if (warning is not null) SetStatus(warning, true);
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x0312 && _hotkey?.Matches(wParam) == true)
        {
            if (_capturing) EndCapture("Hotkey unverändert.");
            else Toggle(false);
            handled = true;
        }
        // Session lock, logoff and remote/console disconnect; power suspend and resume.
        if (msg == 0x02B1 && (int)wParam is 2 or 4 or 6 or 7 ||
            msg == 0x0218 && (int)wParam is 4 or 6 or 7 or 18 || msg == 0x0011)
        {
            StopClicking();
            if (_capturing) EndCapture("Bereit · Linksklick an Mausposition");
        }
        return 0;
    }

    private void Toggle(bool fromButton)
    {
        if (_closing) return;
        if (_state != RunState.Idle) { StopClicking(); return; }
        if (_capturing || _hotkey?.IsRegistered != true) return;
        if (!CommitRate()) return;
        if (fromButton)
        {
            _state = RunState.Pending;
            UpdateControls();
            SetStatus("Startet … Maus zum Ziel bewegen.");
            _countdown.Start();
        }
        else BeginClicking();
    }

    private void BeginClicking()
    {
        if (_closing || _engine is null || _hotkey?.IsRegistered != true) return;
        long generation = ++_generation;
        _state = RunState.Running;
        UpdateControls();
        _engine.Start(_settings.ClicksPerSecond, error => Dispatcher.BeginInvoke(() =>
        {
            if (_closing || generation != _generation) return;
            StopClicking();
            SetStatus("Klickausgabe fehlgeschlagen. Ziel prüfen.", true);
        }));
    }

    private void StopClicking()
    {
        ++_generation;
        _countdown.Stop();
        _engine?.Stop();
        _state = RunState.Idle;
        UpdateControls();
    }

    private void UpdateControls()
    {
        bool idle = _state == RunState.Idle;
        RateBox.IsEnabled = RateSlider.IsEnabled = idle && !_capturing;
        HotkeyButton.IsEnabled = idle;
        HotkeyLabel.Text = _capturing ? "Taste drücken …" : HotkeyService.Display(_settings.Hotkey, _settings.Modifiers);
        HotkeyButton.ToolTip = $"{HotkeyService.Display(_settings.Hotkey, _settings.Modifiers)} · Anklicken zum Ändern. Esc bricht ab.";
        ToggleButton.IsEnabled = !_capturing && (_hotkey?.IsRegistered == true || !idle);
        ToggleLabel.Text = idle ? "Starten" : "Stoppen";
        AutomationProperties.SetName(ToggleButton, ToggleLabel.Text);
        ToggleIcon.Data = idle ? StartIcon : StopIcon;
        ToggleButton.Background = idle ? Light : ActiveBackground;
        ToggleButton.Foreground = idle ? Dark : Light;
        ToggleButton.BorderBrush = idle ? Light : ActiveBorder;
        StatusDot.Fill = idle ? IdleDot : ActiveDot;
        if (_capturing) SetStatus("Taste oder Kombination · Esc bricht ab");
        else if (!idle) SetStatus(_state == RunState.Pending ? "Startet … Maus zum Ziel bewegen." : $"Aktiv · {_settings.ClicksPerSecond} Klicks/s · {HotkeyService.Display(_settings.Hotkey, _settings.Modifiers)} stoppt");
        else if (_hotkey?.IsRegistered != true) SetStatus("Hotkey belegt. Bitte einen anderen wählen.", true);
        else SetStatus("Bereit · Linksklick an Mausposition");
    }

    private static SolidColorBrush Gray(byte value) => Frozen(new SolidColorBrush(Color.FromRgb(value, value, value)));
    private static T Frozen<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
    private void SetStatus(string text, bool warning = false)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
        StatusText.Foreground = warning ? WarningText : MutedText;
    }

    private void RateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _syncing) return;
        SetRate((int)Math.Round(e.NewValue));
    }
    private void RateBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_ready || _syncing) return;
        if (TryRate(out int value)) SetRate(value, false);
        else SetStatus("Bitte eine ganze Zahl von 1 bis 100 eingeben.", true);
    }
    private bool TryRate(out int rate) => int.TryParse(RateBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out rate) && SettingsStore.ValidRate(rate);
    private bool CommitRate()
    {
        if (TryRate(out int value)) { SetRate(value); return true; }
        SetStatus("Bitte eine ganze Zahl von 1 bis 100 eingeben.", true);
        return false;
    }
    private void SetRate(int value, bool updateText = true)
    {
        _syncing = true;
        if (updateText) RateBox.Text = value.ToString(CultureInfo.InvariantCulture);
        RateSlider.Value = value;
        _syncing = false;
        if (_hotkey?.IsRegistered == true) SetStatus("Bereit · Linksklick an Mausposition");
        if (_settings.ClicksPerSecond == value) return;
        _settings = _settings with { ClicksPerSecond = value };
        _saveDelay.Stop();
        _saveDelay.Start();
    }
    private void RateBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (TryRate(out _)) return;
        _syncing = true;
        RateBox.Text = _settings.ClicksPerSecond.ToString(CultureInfo.InvariantCulture);
        _syncing = false;
        SetStatus("Ungültiger Wert · letzter Wert wiederhergestellt.", true);
    }
    private void RateBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
        {
            SetRate(Math.Clamp(_settings.ClicksPerSecond + (e.Key == Key.Up ? 1 : -1), 1, 100));
            RateBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter) { CommitRate(); e.Handled = true; }
    }

    private void Hotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing) { EndCapture("Hotkey unverändert."); return; }
        _capturing = true;
        UpdateControls();
        HotkeyButton.Focus();
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        if (e.IsRepeat) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndCapture("Hotkey unverändert."); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        uint modifiers = 0;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
        if (pressed.HasFlag(ModifierKeys.Control)) modifiers |= 2;
        if (pressed.HasFlag(ModifierKeys.Shift)) modifiers |= 4;
        if (pressed.HasFlag(ModifierKeys.Windows)) modifiers |= 8;
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!HotkeyService.IsAllowed(virtualKey, modifiers))
        { SetStatus("Diese Belegung ist reserviert. Andere Taste wählen.", true); return; }
        if (_hotkey?.TrySet(virtualKey, modifiers) != true)
        { SetStatus("Hotkey bereits belegt. Andere Taste wählen.", true); return; }
        _settings = _settings with { Hotkey = virtualKey, Modifiers = modifiers };
        EndCapture("Hotkey gespeichert.");
        SaveSettings();
    }
    private void EndCapture(string message)
    {
        _capturing = false;
        UpdateControls();
        if (_hotkey?.IsRegistered == true) SetStatus(message);
    }
    private void SaveSettings()
    {
        if (_store.Save(_settings)) _saveDelay.Stop();
        else SetStatus("Einstellungen konnten nicht gespeichert werden.", true);
    }
    private void Toggle_Click(object sender, RoutedEventArgs e) => Toggle(true);
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;

        // A visual hit test also sees disabled controls: their space must never
        // turn into a drag surface while the clicker is running.
        DependencyObject? node = VisualTreeHelper.HitTest(this, e.GetPosition(this))?.VisualHit;
        while (node is not null && node != this)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.Primitives.RangeBase
                or System.Windows.Controls.Primitives.Thumb)
                return;
            node = VisualTreeHelper.GetParent(node);
        }

        e.Handled = true;
        DragMove();
    }
    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        _countdown.Stop();
        _saveDelay.Stop();
        _engine?.Dispose();
        _hotkey?.Dispose();
        if (_sessionNotifications) Native.WTSUnRegisterSessionNotification(_handle);
        _source?.RemoveHook(WindowMessage);
        _store.Save(_settings);
        base.OnClosed(e);
    }
}
