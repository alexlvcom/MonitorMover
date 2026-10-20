using System.IO;
using System.Text.Json;

namespace MonitorMover;

public sealed class AppSettings
{
    public UpdateCheckFrequency UpdateCheckFrequency { get; set; } = UpdateCheckFrequency.Daily;

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MonitorMover");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json);
            if (parsed is null)
            {
                return new AppSettings();
            }

            parsed.UpdateCheckFrequency = UpdateSchedule.Normalize(parsed.UpdateCheckFrequency);
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
