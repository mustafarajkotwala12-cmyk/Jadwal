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
    private readonly string _filePath;

    public ThemeService(string? dataDirectory = null)
    {
        var baseDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jadwal", "data");
        
        try
        {
            Directory.CreateDirectory(baseDir);
        }
        catch { }

        _filePath = Path.Combine(baseDir, ThemeFileName);
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
}
