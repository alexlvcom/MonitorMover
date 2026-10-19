using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MonitorMover;

/// <summary>
/// App version info and release notes loaded from embedded CHANGELOG.md.
/// </summary>
public static class Changelog
{
    private static readonly Lazy<(string FullText, List<(string Version, string Date, string[] Notes)> Entries)> _parsed =
        new(LoadFromResource);

    public static (string Version, string Date, string[] Notes)[] Entries => _parsed.Value.Entries.ToArray();

    public static string MarketingVersion =>
        _parsed.Value.Entries.Count > 0 ? _parsed.Value.Entries[0].Version : "1.2.1";

    public static string BuildVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v != null)
            {
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
            return MarketingVersion;
        }
    }

    public static DateTime BuildDate
    {
        get
        {
            try
            {
                string path = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTime(path);
            }
            catch { /* ignore */ }
            return DateTime.MinValue;
        }
    }

    public static string GetReleaseNotes(string version)
    {
        var entry = _parsed.Value.Entries.FirstOrDefault(e => e.Version == version);
        if (entry.Notes != null && entry.Notes.Length > 0)
        {
            return string.Join(Environment.NewLine, entry.Notes);
        }
        return "No release notes available for this version.";
    }

    public static string FormatFull()
    {
        return _parsed.Value.FullText;
    }

    private static (string FullText, List<(string Version, string Date, string[] Notes)> Entries) LoadFromResource()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("CHANGELOG.md")
            ?? asm.GetManifestResourceStream("MonitorMover.CHANGELOG.md");
        string markdown = "";
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            markdown = reader.ReadToEnd();
        }

        var entries = new List<(string Version, string Date, string[] Notes)>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return (markdown, entries);
        }

        var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string? currentVersion = null;
        string? currentDate = null;
        var currentNotes = new List<string>();

        var headerRegex = new Regex(@"^##\s+([0-9]+\.[0-9]+\.[0-9]+)\s+-\s+([0-9]{4}-[0-9]{2}-[0-9]{2})");

        foreach (var line in lines)
        {
            var match = headerRegex.Match(line);
            if (match.Success)
            {
                if (currentVersion != null)
                {
                    entries.Add((currentVersion, currentDate ?? "", currentNotes.ToArray()));
                    currentNotes.Clear();
                }
                currentVersion = match.Groups[1].Value;
                currentDate = match.Groups[2].Value;
                continue;
            }

            if (currentVersion != null)
            {
                if (line.StartsWith("## "))
                {
                    entries.Add((currentVersion, currentDate ?? "", currentNotes.ToArray()));
                    currentVersion = null;
                    currentNotes.Clear();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    currentNotes.Add(line);
                }
            }
        }

        if (currentVersion != null)
        {
            entries.Add((currentVersion, currentDate ?? "", currentNotes.ToArray()));
        }

        return (markdown, entries);
    }
}
