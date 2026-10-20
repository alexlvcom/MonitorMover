using System.Windows;
using AlexLV.WindowsUi;
using MonitorMover.Ui;

namespace MonitorMover;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Sets per-monitor (v2) DPI awareness before any window, so window coordinates are physical
        // pixels for both WPF and the Win32 window-moving code (app.manifest declares the same).
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();
        ThemeManager.Initialize(AppInfo.DisplayName, AppInfo.Accent, settings.Theme);
        // A pin made from an old portable copy keeps launching it; point pins at this installed EXE.
        _ = Task.Run(() => ShortcutRepair.RepairTaskbarPins());
        var app = Application.Current!;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/MonitorMover;component/Ui/AppStyles.xaml", UriKind.Relative),
        });
        if (AppIcon.Default is { } icon) ThemeManager.WindowIcon = WinFormsHosting.ToImageSource(icon);
        app.DispatcherUnhandledException += (_, e) =>
        {
            ErrorLog.Write("Unhandled UI exception.", e.Exception);
            MessageDialog.Show($"Something went wrong and Monitor Mover could not finish that action.\n\n{e.Exception.Message}\n\n" +
                               $"Details were written to {ErrorLog.FilePath}.", MessageKind.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorLog.Write("Unhandled exception.", e.ExceptionObject as Exception);

        // Headless CLI mode: apply a profile without showing the UI.
        //   MonitorMover.exe --apply "Office"
        //   MonitorMover.exe --list
        if (args.Length > 0 && RunCli(args)) return;

        using var mutex = new Mutex(initiallyOwned: true, name: "MonitorMover.Singleton", createdNew: out var createdNew);
        if (!createdNew)
        {
            var hwnd = NativeMethods.FindWindow(null, AppInfo.MainWindowTitle);
            if (hwnd != IntPtr.Zero) NativeMethods.SetForegroundWindow(hwnd);
            return;
        }

        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        app.Run(new MainWindow(settings));
    }

    /// <summary>Returns true if a CLI command was handled (so the GUI should not start).</summary>
    private static bool RunCli(string[] args)
    {
        var store = new ProfileStore();

        switch (args[0].ToLowerInvariant())
        {
            case "--list":
            case "-l":
                var names = store.Profiles.Count == 0
                    ? "(no profiles saved)"
                    : string.Join(Environment.NewLine, store.Profiles.Select(p => $"  {p.Name}  ({p.Rules.Count} rules)"));
                MessageDialog.Show("Saved profiles:\n\n" + names, MessageKind.Info);
                return true;

            case "--apply":
            case "-a":
                if (args.Length < 2)
                {
                    MessageDialog.Show("Usage: MonitorMover.exe --apply \"ProfileName\"", MessageKind.Warning);
                    return true;
                }
                var profile = store.Find(args[1]);
                if (profile == null)
                {
                    MessageDialog.Show($"Profile \"{args[1]}\" not found.", MessageKind.Error);
                    return true;
                }
                var windows = WindowManager.GetWindows();
                var monitors = WindowManager.GetMonitors();
                var log = ProfileStore.Apply(profile, windows, monitors);
                var ok = log.Count(l => l.StartsWith("OK"));
                // Silent success unless something was skipped/failed.
                if (ok < log.Count) LogWindow.Show(null, $"Apply Profile — {profile.Name}", log);
                return true;

            case "--capture":
            case "-c":
                if (args.Length < 2)
                {
                    MessageDialog.Show("Usage: MonitorMover.exe --capture \"ProfileName\"", MessageKind.Warning);
                    return true;
                }
                var capMon = WindowManager.GetMonitors();
                var capWin = WindowManager.GetWindows();
                var captured = ProfileStore.CaptureCurrent(args[1], capWin, capMon);
                // Updating an existing profile keeps the rules of apps that are closed,
                // exactly as the GUI does — a capture must not drop what it cannot see.
                var previous = store.Find(args[1]);
                if (previous != null) captured = ProfileStore.MergeCapture(previous, captured);
                store.AddOrReplace(captured);
                return true;

            case "--movetest":
            {
                // Diagnostic: move the first window matching arg[1] to the next monitor,
                // logging before/after. Runs in-process (consistent DPI coordinates).
                var mtMons = WindowManager.GetMonitors();
                var mtWins = WindowManager.GetWindows();
                var match = args.Length > 1
                    ? mtWins.FirstOrDefault(w => w.Title.Contains(args[1], StringComparison.OrdinalIgnoreCase))
                    : null;
                var mtSb = new System.Text.StringBuilder();
                if (match == null) { mtSb.AppendLine("no matching window"); }
                else
                {
                    var cur = WindowManager.MonitorForWindow(match, mtMons);
                    var next = mtMons[(cur.Index + 1) % mtMons.Count];
                    mtSb.AppendLine($"BEFORE: \"{match.Title}\" state={match.State} on monitor #{cur.Index + 1} ({cur.DeviceName})");
                    mtSb.AppendLine($"        bounds={match.Bounds}");
                    mtSb.AppendLine($"MOVE  -> next monitor #{next.Index + 1} ({next.DeviceName})");
                    WindowManager.MoveToMonitorKeepRelative(match, next, mtMons);
                    Thread.Sleep(500);
                    var after = WindowManager.GetWindows().FirstOrDefault(w => w.Handle == match.Handle);
                    if (after != null)
                    {
                        var am = WindowManager.MonitorForWindow(after, WindowManager.GetMonitors());
                        mtSb.AppendLine($"AFTER:  state={after.State} on monitor #{am.Index + 1} ({am.DeviceName})");
                        mtSb.AppendLine($"        bounds={after.Bounds}");
                        mtSb.AppendLine(am.DeviceName == next.DeviceName ? "RESULT: OK moved to next monitor" : "RESULT: FAIL still on original");
                    }
                }
                File.WriteAllText(args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "movetest.txt"), mtSb.ToString());
                return true;
            }

            case "--dump":
                // Diagnostic: write detected monitors + windows to a file.
                var mons = WindowManager.GetMonitors();
                var wins = WindowManager.GetWindows();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"MONITORS ({mons.Count}):");
                foreach (var m in mons)
                    sb.AppendLine($"  #{m.Index + 1} {m.ResolutionText} primary={m.IsPrimary} bounds={m.Bounds} dev={m.DeviceName}");
                sb.AppendLine($"\nWINDOWS ({wins.Count}):");
                foreach (var w in wins)
                    sb.AppendLine($"  [{w.ProcessName}] \"{w.Title}\" mon={w.MonitorDeviceName} live={w.LeftTop} eff={w.EffectiveBounds.Left},{w.EffectiveBounds.Top} {w.EffectiveBounds.Width}x{w.EffectiveBounds.Height} {w.State}");
                var outPath = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "monitormover-dump.txt");
                File.WriteAllText(outPath, sb.ToString());
                return true;

            default:
                return false; // fall through to GUI
        }
    }
}
