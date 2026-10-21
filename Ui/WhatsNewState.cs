using System.IO;

namespace MonitorMover.Ui;

/// <summary>Per-PC release acknowledgement, separate from settings drafts and Drive sync.</summary>
internal sealed class WhatsNewState(string? path = null, string? settingsDirectory = null)
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorMover", "last-seen-release.txt");
    private readonly string _settingsDirectory = settingsDirectory ?? AppSettings.SettingsDirectory;

    public bool ShouldShow(string currentVersion)
    {
        if (!Version.TryParse(currentVersion, out var current)) return false;
        try
        {
            return !File.Exists(_path) || !Version.TryParse(File.ReadAllText(_path).Trim(), out var previous) || current > previous;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }

    /// <summary>The release this PC last acknowledged, or null for a fresh installation.</summary>
    public Version? PreviousRelease()
    {
        try
        {
            if (File.Exists(_path)) return Version.TryParse(File.ReadAllText(_path).Trim(), out var seen) ? seen : null;
            // Releases before 1.3.1 kept no record, so their saved settings and profiles tell an upgrade from a fresh
            // installation. 1.3.0 added the Theme setting; anything older is treated as 1.2.2, the last release before it.
            var settings = Path.Combine(_settingsDirectory, "settings.json");
            if (File.Exists(settings) && File.ReadAllText(settings).Contains("\"Theme\"", StringComparison.Ordinal)) return new Version(1, 3, 0);
            if (File.Exists(settings) || File.Exists(Path.Combine(_settingsDirectory, "profiles.json"))) return new Version(1, 2, 2);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public void MarkShown(string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, version);
        File.Move(temp, _path, overwrite: true);
    }
}
