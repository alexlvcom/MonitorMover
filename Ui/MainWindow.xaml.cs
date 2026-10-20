using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>
/// The main window: the monitors present, the movable windows on them, the saved profiles and
/// every move and profile action. All work happens on the UI thread, as it did in WinForms.
/// </summary>
public partial class MainWindow : ThemedWindow
{
    private readonly ProfileStore _store = new();
    private readonly AppSettings _settings;
    private readonly UpdateService _updates = new();
    private readonly CancellationTokenSource _updateCancellation = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(15) };

    /// <summary>Per-app small icons for the window list, keyed by exe path or window handle.</summary>
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);

    private List<MonitorInfo> _monitors = new();
    private List<WindowInfo> _windows = new();

    /// <summary>Device name of the monitor whose windows are shown, or null for all.</summary>
    private string? _monitorFilter;

    private bool _populating;
    private DateTime _nextUpdateCheckUtc = DateTime.MaxValue;
    private bool _checkingUpdate;
    private Version? _lastOfferedUpdate;
    private readonly UpdateStatusTracker _updateStatus = new(Changelog.MarketingVersion);
    private ShutdownListenerWindow? _listenerWindow;
    private bool _cleanedUp;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        Title = AppInfo.MainWindowTitle;

        _updateTimer.Tick += async (_, _) =>
        {
            if (DateTime.UtcNow < _nextUpdateCheckUtc) return;
            await CheckForUpdatesAsync(manual: false);
            _nextUpdateCheckUtc = UpdateSchedule.NextCheck(DateTime.UtcNow, _settings.UpdateCheckFrequency);
        };

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
        Closed += (_, _) => CleanUp();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshAll();
        UpdateSelectionText();

        // Setup and uninstall close the app through WM_CLOSE on this hidden window.
        _listenerWindow = new ShutdownListenerWindow(() => Dispatcher.BeginInvoke(Close));
        _listenerWindow.Show();

        ApplyUpdateSchedule();
        if (_settings.UpdateCheckFrequency != UpdateCheckFrequency.Never) _ = CheckForUpdatesAsync(manual: false);
    }

    /// <summary>Releases timers, the update client and the shutdown listener. Safe to call twice.</summary>
    private void CleanUp()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        _updateTimer.Stop();
        _updateCancellation.Cancel();
        _updateCancellation.Dispose();
        _updates.Dispose();
        _listenerWindow?.Dispose();
        _listenerWindow = null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5:
                RefreshAll();
                e.Handled = true;
                break;
            case Key.F7:
                MoveSelectedToPrimary();
                e.Handled = true;
                break;
            case Key.F8:
                MoveSelectedToNext();
                e.Handled = true;
                break;
            // Type-to-filter keys work wherever focus sits outside a field or menu, so a refresh that
            // briefly takes focus off the list cannot swallow them.
            case Key.Escape when FilterBox.Text.Length > 0 && FocusIsOutsideInput():
                FilterBox.Clear();
                KeepTypingInList();
                e.Handled = true;
                break;
            case Key.Back when FilterBox.Text.Length > 0 && FocusIsOutsideInput():
                FilterBox.Text = FilterBox.Text[..^1];
                KeepTypingInList();
                e.Handled = true;
                break;
        }
    }

    private static bool FocusIsOutsideInput() =>
        Keyboard.FocusedElement is not (TextBox or ComboBox or ComboBoxItem or MenuItem);

    private void SetStatus(string text) => StatusText.Text = text;

    // ------------------------------------------------------------- Data refresh

    private void RefreshAll()
    {
        _monitors = WindowManager.GetMonitors();
        _windows = WindowManager.GetWindows();
        PopulateMonitors();
        PopulateWindows();
        PopulateProfiles();
        SetStatus($"{_monitors.Count} monitor(s), {_windows.Count} window(s)  —  profiles: {_store.FilePath}");
    }

    private void PopulateMonitors()
    {
        _populating = true;
        try
        {
            var rows = _monitors.Select(m => new MonitorRow(m)).ToList();
            MonitorsGrid.ItemsSource = rows;
            MonitorsGrid.SelectedItem = rows.FirstOrDefault(r => r.Info.DeviceName == _monitorFilter);
            // Just tall enough for the monitors present, so the window list gets the rest.
            MonitorsGrid.Height = 38 + MonitorsGrid.RowHeight * Math.Max(2, rows.Count);
            MonitorCount.Text = rows.Count == 1 ? "1 monitor" : $"{rows.Count} monitors";
        }
        finally
        {
            _populating = false;
        }
    }

    private void PopulateWindows()
    {
        // Drop a stale filter if that monitor is gone (e.g. after a layout change).
        if (_monitorFilter != null && _monitors.All(m => m.DeviceName != _monitorFilter)) _monitorFilter = null;

        // The monitor filter and the free-text filter (title or process name) combine with AND.
        var text = FilterBox.Text.Trim();
        var shown = _windows.Where(w =>
                (_monitorFilter == null || w.MonitorDeviceName == _monitorFilter) &&
                (text.Length == 0 ||
                 w.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                 w.ProcessName.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        ScopeText.Text = _monitorFilter == null
            ? "all monitors"
            : _monitors.FirstOrDefault(m => m.DeviceName == _monitorFilter) is { } fm
                ? $"monitor #{fm.Index + 1} ({fm.ResolutionText})"
                : "monitor";
        var total = _monitorFilter == null ? _windows.Count : _windows.Count(w => w.MonitorDeviceName == _monitorFilter);
        WindowCount.Text = text.Length > 0 ? $"{shown.Count} of {total} match \"{text}\"" : $"{shown.Count} of {total}";

        WindowsGrid.ItemsSource = shown.Select(w => new WindowRow(w, MonitorLabel(w), IconFor(w))).ToList();
        UpdateSelectionText();
    }

    private string MonitorLabel(WindowInfo w) =>
        _monitors.FirstOrDefault(m => m.DeviceName == w.MonitorDeviceName) is { } mon ? $"#{mon.Index + 1}" : "?";

    /// <summary>
    /// The app icon for a window, extracted once and cached. Keyed by exe path so all windows of
    /// an app share one icon; windows with no accessible path fall back to a per-handle key.
    /// </summary>
    private ImageSource? IconFor(WindowInfo w)
    {
        var key = !string.IsNullOrEmpty(w.ProcessPath) ? w.ProcessPath : "hwnd:" + w.Handle;
        if (_icons.TryGetValue(key, out var cached)) return cached;

        ImageSource? source = null;
        using (var icon = AppIcons.For(w))
        {
            if (icon != null)
            {
                try { source = WinFormsHosting.ToImageSource(icon); }
                catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException) { }
            }
        }
        _icons[key] = source;
        return source;
    }

    private void PopulateProfiles()
    {
        var current = ProfileCombo.SelectedItem as string;
        var names = _store.Profiles.Select(p => p.Name).OrderBy(n => n).ToList();
        ProfileCombo.ItemsSource = names;
        if (current != null && names.Contains(current)) ProfileCombo.SelectedItem = current;
        else if (names.Count > 0) ProfileCombo.SelectedIndex = 0;
    }

    // ------------------------------------------------------------- Selection and filtering

    private List<WindowInfo> SelectedWindows() => WindowsGrid.SelectedItems.Cast<WindowRow>().Select(r => r.Info).ToList();

    private Profile? SelectedProfile() => ProfileCombo.SelectedItem is string name ? _store.Find(name) : null;

    private void UpdateSelectionText()
    {
        var count = WindowsGrid.SelectedItems.Count;
        SelectionText.Text = count switch { 0 => "No window selected", 1 => "1 window selected", _ => $"{count} windows selected" };
    }

    private void MonitorsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Click a monitor -> show only its windows below.
        if (_populating || MonitorsGrid.SelectedItem is not MonitorRow row) return;
        _monitorFilter = row.Info.DeviceName;
        PopulateWindows();
    }

    private void ShowMonitorWindows_Click(object sender, RoutedEventArgs e)
    {
        if (MonitorsGrid.SelectedItem is not MonitorRow row) return;
        _monitorFilter = row.Info.DeviceName;
        PopulateWindows();
    }

    private void ShowAllWindows_Click(object sender, RoutedEventArgs e)
    {
        _monitorFilter = null;
        _populating = true;
        MonitorsGrid.SelectedItem = null;
        _populating = false;
        PopulateWindows();
    }

    private void WindowsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionText();

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterPlaceholder.Visibility = FilterBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PopulateWindows();
    }

    private void FilterBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Esc in the box clears the filter.
        if (e.Key != Key.Escape || FilterBox.Text.Length == 0) return;
        FilterBox.Clear();
        e.Handled = true;
    }

    /// <summary>Type-to-filter: printable keys typed over the list go into the filter box.</summary>
    private void WindowsGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl)) return;
        FilterBox.Text += e.Text;
        FilterBox.CaretIndex = FilterBox.Text.Length;
        KeepTypingInList();
        e.Handled = true;
    }

    /// <summary>
    /// Filtering replaces the list's rows, which takes keyboard focus off the grid. Give it back,
    /// so the next key typed over the list (or Esc / Backspace) still reaches the filter.
    /// </summary>
    private void KeepTypingInList() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!FilterBox.IsKeyboardFocused) WindowsGrid.Focus();
        });

    /// <summary>Right-click selects the row under the pointer first, as the WinForms list did.</summary>
    private void WindowsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not DataGridRow row) continue;
            if (!row.IsSelected)
            {
                WindowsGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
            row.Focus();
            return;
        }
    }

    private void WindowsMenu_Opened(object sender, RoutedEventArgs e)
    {
        // Populate the "Move To Monitor" submenu on open.
        MoveToMonitorMenu.Items.Clear();
        foreach (var m in _monitors)
        {
            var target = m;
            var item = new MenuItem { Header = $"Monitor #{m.Index + 1} ({m.ResolutionText})" };
            item.Click += (_, _) => MoveSelectedToMonitor(target);
            MoveToMonitorMenu.Items.Add(item);
        }
        MoveToMonitorMenu.IsEnabled = _monitors.Count > 0;
    }

    // ------------------------------------------------------------- Move actions

    private void MoveNext_Click(object sender, RoutedEventArgs e) => MoveSelectedToNext();

    private void MovePrimary_Click(object sender, RoutedEventArgs e) => MoveSelectedToPrimary();

    private void MoveSelectedToNext()
    {
        var sel = SelectedWindows();
        if (sel.Count == 0 || _monitors.Count < 2) { SetStatus("Select a window; need ≥2 monitors."); return; }

        foreach (var w in sel)
        {
            var cur = WindowManager.MonitorForWindow(w, _monitors);
            var next = _monitors[(cur.Index + 1) % _monitors.Count];
            WindowManager.MoveToMonitorKeepRelative(w, next, _monitors);
        }
        RefreshAll();
        SetStatus($"Moved {sel.Count} window(s) to next monitor.");
    }

    private void MoveSelectedToPrimary()
    {
        var sel = SelectedWindows();
        var primary = WindowManager.PrimaryMonitor(_monitors);
        if (sel.Count == 0 || primary == null) { SetStatus("Select a window first."); return; }

        foreach (var w in sel) WindowManager.MoveToMonitorKeepRelative(w, primary, _monitors);
        RefreshAll();
        SetStatus($"Moved {sel.Count} window(s) to primary monitor.");
    }

    private void MoveSelectedToMonitor(MonitorInfo target)
    {
        var sel = SelectedWindows();
        if (sel.Count == 0) { SetStatus("Select a window first."); return; }
        foreach (var w in sel) WindowManager.MoveToMonitorKeepRelative(w, target, _monitors);
        RefreshAll();
        SetStatus($"Moved {sel.Count} window(s) to monitor #{target.Index + 1}.");
    }

    private void BringToFront_Click(object sender, RoutedEventArgs e)
    {
        foreach (var w in SelectedWindows())
        {
            NativeMethods.ShowWindow(w.Handle, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(w.Handle);
        }
    }

    // ------------------------------------------------------------- Profile actions

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedProfile();
        if (selected == null)
        {
            SaveCurrentLayoutAsNew();
            return;
        }

        CaptureCurrentLayout(selected.Name, selected);
    }

    private void SaveLayoutAsNew_Click(object sender, RoutedEventArgs e) => SaveCurrentLayoutAsNew();

    private void SaveCurrentLayoutAsNew()
    {
        var name = PromptWindow.Ask(this, "Save Layout", "Name this new profile (e.g. Home, Office):", "Office");
        if (name == null) return;

        var existing = _store.Find(name);
        if (existing != null &&
            MessageDialog.Show($"Profile \"{name}\" exists. Overwrite?", MessageKind.Question, MessageButtons.YesNo,
                owner: this, title: "Confirm") != MessageResult.Yes)
            return;

        CaptureCurrentLayout(name, existing);
    }

    /// <summary>
    /// Capture the live layout into <paramref name="name"/>. When it updates an existing
    /// profile the capture is merged into it — apps that are closed keep their rules — and
    /// the editor compares the result against the saved version, so the user sees exactly
    /// what will change and cannot overwrite it with no changes.
    /// </summary>
    private void CaptureCurrentLayout(string name, Profile? replaced = null)
    {
        // Capture what is on screen now, not what the list last showed.
        _windows = WindowManager.GetWindows();
        _monitors = WindowManager.GetMonitors();

        var profile = ProfileStore.CaptureCurrent(name, _windows, _monitors);
        if (replaced != null) profile = ProfileStore.MergeCapture(replaced, profile);

        // Let the user immediately prune the captured list.
        var editor = new ProfileEditorWindow(profile, _monitors, replaced, RunningProcesses()) { Owner = this };
        if (editor.ShowDialog() != true) return;

        // Treat changing the name while updating as a rename, not as a duplicate.
        if (replaced != null && !editor.Result.Name.Equals(replaced.Name, StringComparison.OrdinalIgnoreCase))
            _store.Remove(replaced.Name);

        _store.AddOrReplace(editor.Result);
        PopulateProfiles();
        ProfileCombo.SelectedItem = editor.Result.Name;

        var enabled = editor.Result.Rules.Count(r => r.Enabled);
        SetStatus(replaced == null
            ? $"Saved profile \"{editor.Result.Name}\" with {enabled} enabled app rule(s)."
            : $"Saved \"{editor.Result.Name}\": {editor.ChangeCount} change(s), {enabled} enabled app rule(s).");
    }

    /// <summary>Executables with a window open right now — lets the editor point out which
    /// rules belong to apps that are simply not running.</summary>
    private ISet<string> RunningProcesses() => _windows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedWindows();
        if (sel.Count == 0) { SetStatus("Select one or more windows first."); return; }

        var name = PromptWindow.Ask(this, "Add to Profile", "Add selected windows to which profile? (new or existing name):",
            SelectedProfile()?.Name ?? "Office");
        if (name == null) return;

        var profile = _store.Find(name) ?? new Profile { Name = name };
        var captured = ProfileStore.CaptureCurrent(name, sel, _monitors);

        // Merge: rules are per executable, so a captured rule replaces the profile's
        // existing rule for that .exe. Rules narrowed by title are left in place.
        foreach (var rule in captured.Rules)
        {
            profile.Rules.RemoveAll(r =>
                r.ProcessName.Equals(rule.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(r.TitleContains));
            profile.Rules.Add(rule);
        }
        if (string.IsNullOrEmpty(profile.MonitorSignature)) profile.MonitorSignature = captured.MonitorSignature;

        _store.AddOrReplace(profile);
        PopulateProfiles();
        ProfileCombo.SelectedItem = profile.Name;
        SetStatus($"Added {sel.Count} window(s) to profile \"{profile.Name}\".");
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile();
        if (profile == null) { SetStatus("Select a profile first."); return; }

        // The editor mutates the profile in place, so compare against a snapshot of the
        // saved state — that also keeps Save disabled until an edit really changes something.
        var saved = ProfileStore.Clone(profile);
        var editor = new ProfileEditorWindow(profile, _monitors, saved, RunningProcesses()) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            // Cancel: put the in-memory profile back as it was saved.
            _store.Load();
            return;
        }

        if (!editor.Result.Name.Equals(saved.Name, StringComparison.OrdinalIgnoreCase)) _store.Remove(saved.Name);

        _store.AddOrReplace(editor.Result);
        PopulateProfiles();
        ProfileCombo.SelectedItem = editor.Result.Name;
        SetStatus($"Updated profile \"{editor.Result.Name}\": {editor.ChangeCount} change(s).");
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile();
        if (profile == null) { SetStatus("Select a profile first."); return; }

        // Use a fresh window snapshot so we target what is open right now.
        _windows = WindowManager.GetWindows();
        _monitors = WindowManager.GetMonitors();

        var log = ProfileStore.Apply(profile, _windows, _monitors);
        RefreshAll();

        // One line per window, since a rule can place several instances of an app.
        var ok = log.Count(l => l.StartsWith("OK"));
        var skipped = log.Count - ok;
        SetStatus($"Applied \"{profile.Name}\": {ok} window(s) placed" + (skipped > 0 ? $", {skipped} rule(s) skipped." : "."));
        LogWindow.Show(this, $"Apply Profile — {profile.Name}", log);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile();
        if (profile == null) { SetStatus("Select a profile first."); return; }
        if (MessageDialog.Show($"Delete profile \"{profile.Name}\"?", MessageKind.Warning, MessageButtons.YesNo,
                owner: this, defaultIsNegative: true, title: "Confirm") != MessageResult.Yes) return;

        _store.Remove(profile.Name);
        PopulateProfiles();
        SetStatus($"Deleted profile \"{profile.Name}\".");
    }

    private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_store.FilePath}\"");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageDialog.Show($"Could not open the profiles folder.\n\n{ex.Message}", MessageKind.Warning, owner: this);
        }
    }

    // ------------------------------------------------------------- Menus: app

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAll();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    /// <summary>About lives inside Settings, as every tabbed AlexLV Settings window does.</summary>
    private void About_Click(object sender, RoutedEventArgs e) => ShowSettings("About");

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(manual: true);

    private void ShowSettings(string? initialTab = null)
    {
        // A copy, so Cancel really cancels.
        var window = new SettingsWindow(_settings.Clone(), _updateStatus.Status, saved =>
        {
            try
            {
                saved.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return ex.Message;
            }

            // Applied only after the write succeeded, so a failed save leaves the running app as it was.
            _settings.UpdateCheckFrequency = saved.UpdateCheckFrequency;
            _settings.Theme = saved.Theme;
            ApplyUpdateSchedule();
            ThemeManager.Apply(_settings.Theme);
            return null;
        }, () => CheckForUpdatesAsync(manual: true, reportInPlace: true), InstallAvailableUpdate) { Owner = this };
        if (initialTab != null) window.SelectTab(initialTab);
        window.ShowDialog();
    }

    private void ApplyUpdateSchedule()
    {
        _settings.UpdateCheckFrequency = UpdateSchedule.Normalize(_settings.UpdateCheckFrequency);
        _updateTimer.Stop();
        _nextUpdateCheckUtc = UpdateSchedule.NextCheck(DateTime.UtcNow, _settings.UpdateCheckFrequency);
        if (_settings.UpdateCheckFrequency != UpdateCheckFrequency.Never) _updateTimer.Start();
    }

    /// <summary>
    /// Checks GitHub. Manual checks report every outcome; automatic ones only offer an update. A check from
    /// Settings → Updates (<paramref name="reportInPlace"/>) shows "latest version" and failures in its status
    /// card only, so only a newer release opens a dialog.
    /// </summary>
    private async Task<UpdateStatus> CheckForUpdatesAsync(bool manual, bool reportInPlace = false)
    {
        var current = Changelog.MarketingVersion;
        if (_checkingUpdate) return new UpdateStatus(UpdateState.Checking, "Checking for updates…", "A check is already running");
        if (_cleanedUp || (!manual && _settings.UpdateCheckFrequency == UpdateCheckFrequency.Never))
            return new UpdateStatus(UpdateState.NotChecked, "Automatic checks are off", $"Version {current}");

        _checkingUpdate = true;
        try
        {
            var update = await _updates.CheckAsync(Version.Parse(current), _updateCancellation.Token);
            if (_cleanedUp) return _updateStatus.Status;

            if (update is null)
            {
                _updateStatus.RecordCurrent(DateTime.Now);
                if (manual && !reportInPlace)
                    MessageDialog.Show(UpdateMessages.Latest(current) + ".", MessageKind.Info, owner: ActiveOwner());
                return _updateStatus.Status;
            }

            _updateStatus.RecordNewer(update, DateTime.Now);
            if (!manual && _lastOfferedUpdate == update.Version) return _updateStatus.Status;
            _lastOfferedUpdate = update.Version;
            ShowUpdateWindow(update);
            return _updateStatus.Status;
        }
        catch (OperationCanceledException)
        {
            return new UpdateStatus(UpdateState.Failed, "Update check stopped", $"Version {current}");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException
                                      or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (_cleanedUp) return _updateStatus.Status;
            // Remembered, so Settings opened later shows this failure rather than an older "latest version".
            _updateStatus.RecordFailed(ex.Message, DateTime.Now);
            if (manual && !reportInPlace)
                MessageDialog.Show($"Could not check for updates. Please try again later.\n\n{ex.Message}",
                    MessageKind.Warning, owner: ActiveOwner());
            return _updateStatus.Status;
        }
        finally { _checkingUpdate = false; }
    }

    private void ShowUpdateWindow(AppUpdate update) =>
        new UpdateWindow(update, _updates, _updateCancellation.Token) { Owner = ActiveOwner() }.ShowDialog();

    /// <summary>The Settings card's Update action, for the release the last check found.</summary>
    private void InstallAvailableUpdate()
    {
        if (_updateStatus.Available is { } update) ShowUpdateWindow(update);
    }

    /// <summary>The window a dialog belongs to: Settings while it is open, otherwise this window.</summary>
    private Window ActiveOwner() =>
        OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsActive) ?? OwnedWindows.Cast<Window>().LastOrDefault() ?? this;

    // ------------------------------------------------------------- Row models

    public sealed record MonitorRow(MonitorInfo Info)
    {
        public string Monitor => $"#{Info.Index + 1}";
        public string Resolution => Info.ResolutionText;
        public string LeftTop => $"{Info.Bounds.Left}, {Info.Bounds.Top}";
        public string RightBottom => $"{Info.Bounds.Right}, {Info.Bounds.Bottom}";
        public string Primary => Info.IsPrimary ? "Yes" : "No";
        public string WorkArea => $"{Info.WorkingArea.Width}x{Info.WorkingArea.Height} @ {Info.WorkingArea.Left},{Info.WorkingArea.Top}";
        public string Colors => Info.BitsPerPixel > 0 ? Info.BitsPerPixel.ToString() : "";
        public string Device => Info.DeviceName;
    }

    public sealed record WindowRow(WindowInfo Info, string Monitor, ImageSource? Icon)
    {
        public string Title => Info.Title;
        public string LeftTop => Info.LeftTop;
        public string Size => Info.SizeText;
        public string State => Info.State.ToString();
        public string Class => Info.ClassName;
        public string Process => Info.ProcessName;
        public string Path => Info.ProcessPath;
    }
}
