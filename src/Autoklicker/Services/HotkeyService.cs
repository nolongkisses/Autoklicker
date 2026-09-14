using System.Windows.Input;

namespace Autoklicker.Services;

public sealed class HotkeyService(nint window) : IDisposable
{
    private int _id = 0x4100;
    public bool IsRegistered { get; private set; }
    public uint Key { get; private set; }
    public uint Modifiers { get; private set; }
    public bool Matches(nint id) => IsRegistered && (int)id == _id;

    public static bool IsAllowed(uint key, uint modifiers)
    {
        if ((modifiers & ~7u) != 0) return false;
        // Function keys F1–F11/F13–F24, letters, digits, numpad and navigation.
        bool supported = key is >= 0x70 and <= 0x87 && key != 0x7B ||
                         key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x60 and <= 0x6F ||
                         key is 0x08 or 0x09 or 0x0D or 0x13 or 0x20 or >= 0x21 and <= 0x28 or 0x2D or 0x2E;
        if (!supported) return false;
        // Protect common operating-system commands, including Alt+F4 and Ctrl+Alt+Delete.
        if ((modifiers & 1) != 0 && key is 0x09 or 0x73 or 0x20) return false;
        if ((modifiers & 3) == 3 && key == 0x2E) return false;
        return true;
    }

    public bool TrySet(uint key, uint modifiers)
    {
        if (!IsAllowed(key, modifiers)) return false;
        if (IsRegistered && key == Key && modifiers == Modifiers) return true;
        int nextId = _id == 0x4100 ? 0x4101 : 0x4100;
        if (!Native.RegisterHotKey(window, nextId, modifiers | 0x4000, key)) return false;
        // Register first: a conflict must never discard the working stop hotkey.
        if (IsRegistered) Native.UnregisterHotKey(window, _id);
        _id = nextId;
        Key = key;
        Modifiers = modifiers;
        IsRegistered = true;
        return true;
    }

    public void Suspend()
    {
        if (IsRegistered) Native.UnregisterHotKey(window, _id);
        IsRegistered = false;
    }

    public static string Display(uint key, uint modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Strg");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        string name = KeyInterop.KeyFromVirtualKey((int)key).ToString();
        if (key is >= 0x30 and <= 0x39) name = ((char)key).ToString();
        name = name switch { "Space" => "Leertaste", "Return" => "Enter", "Delete" => "Entf", "Insert" => "Einfg", "Back" => "Rücktaste", _ => name };
        parts.Add(name);
        return string.Join(" + ", parts);
    }

    public void Dispose() => Suspend();
}
