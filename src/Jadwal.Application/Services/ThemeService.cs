using System.Text.Json;

namespace Jadwal.Application.Services;

public enum JadwalThemeMode
{
    Light,
    Dark,
    System
}

public class ThemeService
{
    private const string ThemeFileName = "theme_preference.json";
    private const string FontSizeFileName = "fontsize_preference.json";
    private const string SyncDisabledFileName = "sync_disabled_preference.json";

    private readonly string _baseDir;
    private readonly string _filePath;

    public static event Action<double>? FontScaleChanged;

    public ThemeService(string? dataDirectory = null)
    {
        _baseDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data");
        
        try
        {
            Directory.CreateDirectory(_baseDir);
        }
        catch { }

        _filePath = Path.Combine(_baseDir, ThemeFileName);
    }

    public JadwalThemeMode GetSavedTheme()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var text = File.ReadAllText(_filePath).Trim();
                if (Enum.TryParse<JadwalThemeMode>(text, true, out var mode))
                    return mode;
            }
        }
        catch { }

        // Default to authentic Light Fatimid theme per user request
        return JadwalThemeMode.Light;
    }

    public void SaveTheme(JadwalThemeMode mode)
    {
        try
        {
            File.WriteAllText(_filePath, mode.ToString());
        }
        catch { }
    }

    public string GetSavedFontSize()
    {
        try
        {
            var path = Path.Combine(_baseDir, FontSizeFileName);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (text is "Normal" or "Large" or "ExtraLarge")
                    return text;
            }
        }
        catch { }

        return "Normal";
    }

    public void SaveFontSize(string fontSize)
    {
        try
        {
            var path = Path.Combine(_baseDir, FontSizeFileName);
            File.WriteAllText(path, fontSize);
        }
        catch { }
    }

    public double GetSavedFontScale()
    {
        return GetSavedFontSize() switch
        {
            "Large" => 1.15,
            "ExtraLarge" => 1.30,
            _ => 1.0
        };
    }

    public void NotifyFontScaleChanged(double scale)
    {
        FontScaleChanged?.Invoke(scale);
    }

    public bool GetSavedSyncDisabled()
    {
        try
        {
            var path = Path.Combine(_baseDir, SyncDisabledFileName);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (bool.TryParse(text, out var val))
                    return val;
            }
        }
        catch { }

        return false;
    }

    public void SaveSyncDisabled(bool disabled)
    {
        try
        {
            var path = Path.Combine(_baseDir, SyncDisabledFileName);
            File.WriteAllText(path, disabled.ToString());
        }
        catch { }
    }
}
