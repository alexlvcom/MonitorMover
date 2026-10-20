using System.Drawing;
using System.Reflection;

namespace MonitorMover;

/// <summary>
/// MonitorMover's own icon, loaded once from the embedded MonitorMover.ico. Every window shows it
/// (through ThemeManager.WindowIcon) so the title bar and taskbar match what Explorer shows for the
/// executable.
/// </summary>
public static class AppIcon
{
    private static readonly Lazy<Icon?> _icon = new(Load);

    /// <summary>The application icon, or null if the resource is somehow missing.</summary>
    public static Icon? Default => _icon.Value;

    private static Icon? Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MonitorMover.ico");
            // Icon keeps reading from the stream lazily, so copy it into memory first.
            if (stream == null) return null;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return new Icon(buffer);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return null;
        }
    }
}
