using System.IO;
using MonitorMover;
using MonitorMover.Ui;
internal static class ReleaseAcknowledgementTests
{
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ReleaseAcknowledgementTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "last-seen-release.txt");
        static void Check(bool ok, string message = "Release acknowledgement regression") { if (!ok) throw new Exception(message); }
        try
        {
            var state = new WhatsNewState(path, folder);
            Check(state.ShouldShow("1.0.0"));
            state.MarkShown("1.0.0");
            Check(!new WhatsNewState(path).ShouldShow("1.0.0"));
            Check(!state.ShouldShow("0.9.0"));
            Check(state.ShouldShow("1.0.1"));
            File.WriteAllText(path, "corrupt");
            Check(state.ShouldShow("1.0.0"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        PreviousReleaseComesFromTheRecordOrOlderSettings();
        UpgradeShowsEveryReleaseSinceThePreviousOne();
    }

    private static void PreviousReleaseComesFromTheRecordOrOlderSettings()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ReleaseAcknowledgementTests", Guid.NewGuid().ToString("N"));
        var seen = Path.Combine(folder, "local", "last-seen-release.txt");
        var settings = Path.Combine(folder, "settings.json");
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        Directory.CreateDirectory(folder);
        try
        {
            var state = new WhatsNewState(seen, folder);
            Check(state.PreviousRelease() is null, "fresh installation has no previous release");
            File.WriteAllText(Path.Combine(folder, "profiles.json"), "[]");
            Check(state.PreviousRelease() == new Version(1, 2, 2), "profiles without a record mean a release before 1.3.0");
            File.WriteAllText(settings, "{ \"UpdateCheckFrequency\": 1 }");
            Check(state.PreviousRelease() == new Version(1, 2, 2), "settings without Theme mean a release before 1.3.0");
            File.WriteAllText(settings, "{ \"UpdateCheckFrequency\": 1, \"Theme\": 0 }");
            Check(state.PreviousRelease() == new Version(1, 3, 0), "settings with Theme mean 1.3.0");
            state.MarkShown("1.3.1");
            Check(state.PreviousRelease() == new Version(1, 3, 1), "the acknowledgement record wins over settings");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void UpgradeShowsEveryReleaseSinceThePreviousOne()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var entries = Changelog.Parse("# Changelog\n\n## 1.3.2 - 2026-10-21\n\n### Main changes\n\n- Fix\n\n## 1.3.1 - 2026-10-20\n\n### Main changes\n\n- Highlights\n\n" +
                                      "### Other updates\n\n- Detail\n\n## 1.3.0 - 2026-10-20\n\n### Main changes\n\n- Redesign\n\n## 1.2.1 - 2026-10-19\n\n- Timeout\n");
        var upgrade = Changelog.MainChangesSince(entries, new Version(1, 3, 0), "1.3.2");
        Check(upgrade.Select(e => e.Version).SequenceEqual(["1.3.2", "1.3.1"]), "upgrade lists every skipped release, newest first");
        Check(!upgrade.SelectMany(e => e.Notes).Contains("Detail"), "upgrade lists only Main changes");
        var older = Changelog.MainChangesSince(entries, new Version(1, 2, 0), "1.3.2");
        Check(older.Length == 4 && older[^1].Notes.SequenceEqual(["Timeout"]), "a release without Main changes keeps its notes");
        var fresh = Changelog.MainChangesSince(entries, null, "1.3.2");
        Check(fresh.Length == 1 && fresh[0].Notes.SequenceEqual(["Fix"]), "fresh installation shows only the current release");
    }
}
