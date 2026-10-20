using System.IO;
using System.Text.Json;
using AlexLV.WindowsUi;

namespace MonitorMover;

public sealed class AppSettings
{
    public UpdateCheckFrequency UpdateCheckFrequency { get; set; } = UpdateCheckFrequency.Daily;

    /// <summary>Appearance → Theme. System (the default) follows the Windows app mode.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>A separate copy for the Settings draft, so Cancel leaves the running settings untouched.</summary>
    public AppSettings Clone() => new() { UpdateCheckFrequency = UpdateCheckFrequency, Theme = Theme };

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MonitorMover");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= SettingsFilePath;
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json);
            if (parsed is null)
            {
                return new AppSettings();
            }

            parsed.UpdateCheckFrequency = UpdateSchedule.Normalize(parsed.UpdateCheckFrequency);
            parsed.Theme = ThemeManager.Normalize(parsed.Theme);
            return parsed;
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Writes the settings atomically. Throws when the file cannot be written so
    /// Settings can report the failure instead of silently losing the change.
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= SettingsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { }
            throw;
        }
    }
}
