using System.IO;
using System.Text.Json;

namespace Autoklicker.Services;

public sealed record Settings(int ClicksPerSecond = 10, uint Hotkey = 0x75, uint Modifiers = 0);

public sealed class SettingsStore(string? directory = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private Settings? _lastSaved;
    private readonly string _directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autoklicker");
    public static bool ValidRate(int value) => value is >= 1 and <= 100;

    public Settings Load(out string? warning)
    {
        _lastSaved = null;
        warning = null;
        string path = Path.Combine(_directory, "settings.json");
        if (!File.Exists(path)) return new Settings();
        try
        {
            Settings? settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            if (settings is null || !ValidRate(settings.ClicksPerSecond) ||
                !HotkeyService.IsAllowed(settings.Hotkey, settings.Modifiers))
                throw new JsonException();
            return _lastSaved = settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = "Einstellungen zurückgesetzt: Datei nicht lesbar.";
            return new Settings();
        }
    }

    public bool Save(Settings settings)
    {
        if (!ValidRate(settings.ClicksPerSecond) || !HotkeyService.IsAllowed(settings.Hotkey, settings.Modifiers))
            return false;
        if (settings == _lastSaved) return true;
        string? temp = null;
        try
        {
            Directory.CreateDirectory(_directory);
            temp = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, Path.Combine(_directory, "settings.json"), true);
            _lastSaved = settings;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            if (temp is not null)
                try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
