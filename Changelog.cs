using System.Reflection;
using System.Text.RegularExpressions;

namespace MonitorMover;

/// <summary>A heading group of one release ("Main changes", "Other updates"); untitled when the entry has none.</summary>
public sealed record ChangelogSection(string? Heading, IReadOnlyList<string> Bullets);

/// <summary>One release from CHANGELOG.md.</summary>
public sealed record ChangelogEntry(string Version, string Date, IReadOnlyList<ChangelogSection> Sections)
{
    /// <summary>Every bullet of the release, in order.</summary>
    public string[] Notes => Sections.SelectMany(s => s.Bullets).ToArray();
}

/// <summary>
/// Version info and release notes. The running version comes from the assembly (MonitorMover.csproj),
/// and the notes from CHANGELOG.md embedded at build time; nothing is hand-maintained in code, so
/// the title bar, About and the update check cannot drift from the release.
/// </summary>
public static class Changelog
{
    public const string RepositoryUrl = "https://github.com/alexlvcom/MonitorMover";

    private static readonly Lazy<ChangelogEntry[]> Parsed = new(() => Parse(ReadEmbedded()));

    /// <summary>Newest first.</summary>
    public static ChangelogEntry[] Entries => Parsed.Value;

    /// <summary>The running release, e.g. "1.3.0", from the assembly's informational version.</summary>
    public static string MarketingVersion
    {
        get
        {
            var informational = typeof(Changelog).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational)) return informational.Split('+')[0];
            return Entries.Length > 0 ? Entries[0].Version : "0.0.0";
        }
    }

    /// <summary>Full build version (four parts).</summary>
    public static string BuildVersion => typeof(Changelog).Assembly.GetName().Version?.ToString() ?? MarketingVersion;

    /// <summary>The release whose notes About shows: the running version, not merely the newest heading.</summary>
    public static ChangelogEntry? Current => Find(MarketingVersion);

    public static ChangelogEntry? Find(string version) => Entries.FirstOrDefault(e => e.Version == version);

    /// <summary>
    /// The Main changes of every release after <paramref name="previous"/> up to <paramref name="current"/>, newest first.
    /// Without a previous release only the current one is included. A release without a Main changes heading keeps all its notes.
    /// </summary>
    public static ChangelogEntry[] MainChangesSince(IEnumerable<ChangelogEntry> entries, Version? previous, string current)
    {
        if (!Version.TryParse(current, out var target)) return [];
        return entries
            .Where(e => Version.TryParse(e.Version, out var v) && (previous is null ? v == target : v > previous && v <= target))
            .OrderByDescending(e => Version.Parse(e.Version))
            .Select(e =>
            {
                var main = e.Sections.Where(s => string.Equals(s.Heading, "Main changes", StringComparison.OrdinalIgnoreCase)).ToArray();
                return main.Length > 0 ? e with { Sections = main } : e;
            })
            .ToArray();
    }

    /// <summary>Plain-text notes for one release, used by Copy details.</summary>
    public static string GetReleaseNotes(string version)
    {
        var entry = Find(version);
        if (entry is null) return "No release notes available for this version.";

        var lines = new List<string>();
        foreach (var section in entry.Sections)
        {
            if (section.Heading is not null)
            {
                if (lines.Count > 0) lines.Add("");
                lines.Add(section.Heading);
            }
            lines.AddRange(section.Bullets.Select(b => "• " + b));
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Timestamp this executable was built.</summary>
    public static DateTime BuildDate
    {
        get
        {
            try
            {
                // Environment.ProcessPath is the only reliable source in a single-file publish.
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return File.GetLastWriteTime(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            return DateTime.MinValue;
        }
    }

    private static readonly Regex VersionHeading = new(@"^##\s+\[?(?<v>\d+\.\d+\.\d+)\]?\s*[-—–]\s*(?<d>\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

    /// <summary>Parses <c>## 1.3.0 - 2026-10-20</c> headings, <c>###</c> sections and <c>-</c> bullets.</summary>
    public static ChangelogEntry[] Parse(string markdown)
    {
        var entries = new List<ChangelogEntry>();
        string? version = null, date = null, heading = null;
        var sections = new List<ChangelogSection>();
        var bullets = new List<string>();

        void FlushSection()
        {
            if (bullets.Count > 0) sections.Add(new ChangelogSection(heading, bullets.ToArray()));
            bullets.Clear();
        }

        void FlushEntry()
        {
            FlushSection();
            if (version is not null) entries.Add(new ChangelogEntry(version, date!, sections.ToArray()));
            sections.Clear();
            heading = null;
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var match = VersionHeading.Match(line);
            if (match.Success)
            {
                FlushEntry();
                version = match.Groups["v"].Value;
                date = match.Groups["d"].Value;
                continue;
            }

            if (version is null) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushEntry();
                version = null;
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushSection();
                heading = line[4..].Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                bullets.Add(line[2..].Trim().Replace("`", ""));
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal) && bullets.Count > 0 && line.Trim().Length > 0)
            {
                bullets[^1] += " " + line.Trim();
            }
        }

        FlushEntry();
        return entries.ToArray();
    }

    private static string ReadEmbedded()
    {
        using var stream = typeof(Changelog).Assembly.GetManifestResourceStream("CHANGELOG.md");
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
