using AlexLV.WindowsUi;

namespace MonitorMover;

/// <summary>User-visible identity: the spaced display name and the accent taken from the icon's monitor blue.</summary>
public static class AppInfo
{
    public const string DisplayName = "Monitor Mover";

    /// <summary>White on #1F5FBF is 6.2:1; #7AAAF2 is 6.9:1 on the dark surface.</summary>
    public static readonly AccentColors Accent = AccentColors.FromHex("#1F5FBF", "#7AAAF2", onDark: "#0B1A2E");

    /// <summary>The main window title; the version sits at the right of its title row instead.</summary>
    public static string MainWindowTitle => DisplayName;
}
