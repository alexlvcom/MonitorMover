namespace MonitorMover;

/// <summary>Appends unexpected errors to errors.log beside the settings, so a crash leaves a trace.</summary>
public static class ErrorLog
{
    private const long MaxBytes = 512 * 1024;

    public static string FilePath => Path.Combine(AppSettings.SettingsDirectory, "errors.log");

    public static void Write(string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.SettingsDirectory);
            // Bounded: start over once the log grows past MaxBytes.
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes) File.Delete(FilePath);
            File.AppendAllText(FilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} v{Changelog.MarketingVersion} {message}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the app down.
        }
    }
}
