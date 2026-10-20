using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>
/// Lets the user prune and tweak the rules of a captured profile: toggle rows on/off,
/// refine the title match, and pick the target monitor / state.
/// </summary>
/// <remarks>
/// When a <c>baseline</c> (the profile's currently saved state) is supplied, the lower
/// pane shows a live comparison of the edits against it and <b>Save</b> stays disabled
/// until something actually differs — so re-capturing a layout is never a blind
/// overwrite, and a no-op save is impossible.
/// </remarks>
public partial class ProfileEditorWindow : ThemedWindow
{
    private readonly Profile _profile;
    private readonly Profile? _baseline;
    private readonly ISet<string>? _runningProcesses;
    private readonly List<MonitorInfo> _monitors;
    private readonly ObservableCollection<RuleRow> _rows = new();

    public Profile Result => _profile;

    /// <summary>Number of differences between the saved profile and the edited one
    /// (0 when there is no baseline to compare against).</summary>
    public int ChangeCount { get; private set; }

    public ProfileEditorWindow(Profile profile, List<MonitorInfo> monitors, Profile? baseline = null,
        ISet<string>? runningProcesses = null)
    {
        InitializeComponent();
        _profile = profile;
        _baseline = baseline;
        _runningProcesses = runningProcesses;
        _monitors = monitors;

        Title = "Edit Profile";
        NameBox.Text = profile.Name;

        var monitorOptions = monitors.Select(MonitorLabel).ToList();
        foreach (var rule in profile.Rules)
        {
            var row = new RuleRow(rule, monitorOptions, rule.ResolveMonitor(monitors) is { } mon ? MonitorLabel(mon) : null);
            row.PropertyChanged += (_, _) => RefreshDiff();
            _rows.Add(row);
        }
        RulesGrid.ItemsSource = _rows;

        if (baseline is not null)
        {
            // With a baseline the rules and the comparison share the space through a draggable
            // splitter; without one the rules grid simply fills the dialog.
            Height = 780;
            SplitterRow.Height = new GridLength(12);
            DiffRowDefinition.Height = new GridLength(0.75, GridUnitType.Star);
            DiffRowDefinition.MinHeight = 170;
            DiffPanel.Visibility = Visibility.Visible;
        }

        NameBox.TextChanged += (_, _) => RefreshDiff();
        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        RefreshDiff();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.CaretIndex = NameBox.Text.Length;
        };
    }

    /// <summary>Dropdown caption for a monitor. Includes the layout position so two
    /// monitors of the same resolution get distinct entries.</summary>
    private static string MonitorLabel(MonitorInfo m) =>
        $"#{m.Index + 1} ({m.Bounds.Width}x{m.Bounds.Height} @ {m.Bounds.Left},{m.Bounds.Top})" +
        (m.IsPrimary ? " *" : "");

    private void OnThemeChanged(object? sender, EventArgs e) => RefreshDiff();

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in RulesGrid.SelectedItems.Cast<RuleRow>().ToList())
        {
            _profile.Rules.Remove(row.Rule);
            _rows.Remove(row);
        }
        RefreshDiff();
    }

    /// <summary>Push the rows' values into the rule objects.</summary>
    private void SyncRowsToRules()
    {
        foreach (var row in _rows)
        {
            var rule = row.Rule;
            rule.Enabled = row.Enabled;
            rule.TitleContains = row.TitleContains?.Trim() is { Length: > 0 } tc ? tc : null;

            if (row.TargetMonitor is { } label && _monitors.FirstOrDefault(m => MonitorLabel(m) == label) is { } mon)
                rule.SetTargetMonitor(mon);

            if (Enum.TryParse<WinState>(row.State, out var state)) rule.State = state;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _profile.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? _profile.Name : NameBox.Text.Trim();
        SyncRowsToRules();

        // Clearing a title filter can leave two general rules for one .exe; only the
        // first would ever match, so drop the rest.
        _profile.CollapseToOneRulePerProcess();
        DialogResult = true;
    }

    private void ShowUnchanged_Changed(object sender, RoutedEventArgs e) => RefreshDiff();

    private void ShowUnchangedLabel_Click(object sender, MouseButtonEventArgs e)
    {
        // A click on the switch itself already toggled it.
        if (e.OriginalSource is DependencyObject source && IsInside(source, ShowUnchanged)) return;
        ShowUnchanged.IsChecked = ShowUnchanged.IsChecked != true;
    }

    private static bool IsInside(DependencyObject node, DependencyObject ancestor)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == ancestor) return true;
        }

        return false;
    }

    // ------------------------------------------------------------ comparison pane

    public sealed record DiffPalette(
        Color AddedBack, Color AddedFore, Color RemovedBack, Color RemovedFore, Color ChangedBack, Color ChangedFore,
        Color KeptBack, Color KeptFore, Color Quiet, Color BannerChangedBack, Color BannerChangedFore,
        Color BannerCleanBack, Color BannerCleanFore);

    private static readonly DiffPalette LightDiff = new(
        Hex("#E9F7EA"), Hex("#16652B"), Hex("#FDEAEA"), Hex("#A01A1A"), Hex("#FFF8E0"), Hex("#8C5800"),
        Hex("#ECF0F5"), Hex("#465C73"), Hex("#6F6F6F"), Hex("#FFF3CD"), Hex("#7C4D00"), Hex("#DEECFA"), Hex("#0D4785"));

    private static readonly DiffPalette DarkDiff = new(
        Hex("#1E3324"), Hex("#9FD39F"), Hex("#3A2222"), Hex("#F2A198"), Hex("#3A3220"), Hex("#F0C97F"),
        Hex("#252C35"), Hex("#A9BCD0"), Hex("#8C8C8C"), Hex("#3A3220"), Hex("#F0C97F"), Hex("#1F2D3D"), Hex("#9CC4FF"));

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Recompute the comparison against the saved profile and gate Save on it.</summary>
    private void RefreshDiff()
    {
        if (_baseline is null) return;

        SyncRowsToRules();

        var newName = string.IsNullOrWhiteSpace(NameBox.Text) ? _profile.Name : NameBox.Text.Trim();
        var renamed = !newName.Equals(_baseline.Name, StringComparison.Ordinal);

        var rows = new List<LayoutChange>();
        if (renamed)
        {
            rows.Add(new LayoutChange
            {
                Kind = ChangeKind.Changed,
                App = "(profile name)",
                Note = $"{_baseline.Name} → {newName}",
                NoteChanged = true,
            });
        }
        rows.AddRange(ProfileStore.Diff(_baseline, _profile, _monitors, _runningProcesses));

        var changes = rows.Where(r => r.IsChange).ToList();
        var kept = rows.Where(r => r.Kind == ChangeKind.Kept).ToList();
        var unchanged = rows.Count - changes.Count - kept.Count;
        ChangeCount = changes.Count;

        // Changes first, then the closed apps whose rules are being preserved — those are
        // not changes, but the user needs to see the profile is not losing them.
        var shown = changes.Concat(kept);
        if (ShowUnchanged.IsChecked == true) shown = shown.Concat(rows.Where(r => r.Kind == ChangeKind.Unchanged));

        var palette = ThemeManager.IsDark ? DarkDiff : LightDiff;
        DiffGrid.ItemsSource = shown.Select(c => DiffRow.From(c, palette)).ToList();

        var keptText = kept.Count > 0 ? $"{kept.Count} app(s) not running — rules kept" : "";
        var unchangedText = unchanged > 0 ? $"{unchanged} app(s) unchanged" : "";
        var tail = string.Join("   ·   ", new[] { keptText, unchangedText }.Where(s => s.Length > 0));

        if (changes.Count == 0)
        {
            SetBanner(palette.BannerCleanBack, palette.BannerCleanFore, "",
                $"No changes to save — this layout already matches the saved profile \"{_baseline.Name}\"" +
                (tail.Length > 0 ? $"   ({tail})" : ""));
            EmptyNote.Text = "Nothing differs from the saved profile, so Save is disabled.\n" +
                             "Move a window and re-open this dialog, or turn on \"Show unchanged apps\" " +
                             "to review what the profile currently holds.";
        }
        else
        {
            SetBanner(palette.BannerChangedBack, palette.BannerChangedFore, "",
                $"{changes.Count} change(s) to save vs the saved profile \"{_baseline.Name}\"" +
                (tail.Length > 0 ? $"   ({tail})" : ""));
            EmptyNote.Text = "";
        }

        // Show the placeholder instead of an empty table.
        var haveRows = DiffGrid.Items.Count > 0;
        EmptyNote.Visibility = haveRows ? Visibility.Collapsed : Visibility.Visible;
        DiffGrid.Visibility = haveRows ? Visibility.Visible : Visibility.Collapsed;

        // Nothing to write — don't let the user overwrite the profile with itself.
        SaveButton.IsEnabled = changes.Count > 0;
        SaveButton.Content = changes.Count > 0 ? "Save" : "Nothing to Save";
    }

    private void SetBanner(Color back, Color fore, string glyph, string text)
    {
        Banner.Background = Brush(back);
        Banner.BorderBrush = Brush(Color.FromArgb(0x55, fore.R, fore.G, fore.B));
        BannerGlyph.Text = glyph;
        BannerGlyph.Foreground = Brush(fore);
        BannerText.Text = text;
        BannerText.Foreground = Brush(fore);
    }

    /// <summary>One editable rule row.</summary>
    public sealed class RuleRow : INotifyPropertyChanged
    {
        private static readonly IReadOnlyList<string> States = new[] { "Normal", "Maximized", "Minimized" };
        private bool _enabled;
        private string _titleContains;
        private string? _targetMonitor;
        private string _state;

        public RuleRow(AppRule rule, IReadOnlyList<string> monitorOptions, string? targetMonitor)
        {
            Rule = rule;
            MonitorOptions = monitorOptions;
            _enabled = rule.Enabled;
            _titleContains = rule.TitleContains ?? "";
            _targetMonitor = targetMonitor;
            _state = rule.State.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AppRule Rule { get; }
        public IReadOnlyList<string> MonitorOptions { get; }
        public IReadOnlyList<string> StateOptions => States;
        public string Process => Rule.ProcessName;
        public string CapturedTitle => Rule.DisplayTitle;
        public string PosSize => $"{Rule.RelativeX},{Rule.RelativeY}  {Rule.Width}x{Rule.Height}";

        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
        public string TitleContains { get => _titleContains; set => Set(ref _titleContains, value); }
        public string? TargetMonitor { get => _targetMonitor; set => Set(ref _targetMonitor, value); }
        public string State { get => _state; set => Set(ref _state, value); }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>A cell of a changed row: emphasized when that field moved, muted otherwise.</summary>
    public sealed record DiffCell(string Text, Brush Brush, FontWeight Weight);

    public sealed record DiffRow(string Kind, string App, DiffCell Monitor, DiffCell State, DiffCell Position,
        DiffCell Size, DiffCell Note, Brush Back, Brush Fore)
    {
        public static DiffRow From(LayoutChange c, DiffPalette palette)
        {
            var (back, fore) = c.Kind switch
            {
                ChangeKind.Added => (palette.AddedBack, palette.AddedFore),
                ChangeKind.Removed => (palette.RemovedBack, palette.RemovedFore),
                ChangeKind.Changed => (palette.ChangedBack, palette.ChangedFore),
                ChangeKind.Kept => (palette.KeptBack, palette.KeptFore),
                _ => (Colors.Transparent, palette.Quiet),
            };
            var foreBrush = Brush(fore);
            var quiet = Brush(palette.Quiet);

            // In a changed row only some fields moved: embolden those, mute the rest so the
            // eye lands on what actually differs.
            DiffCell Cell(string text, bool changed) => c.Kind != ChangeKind.Changed
                ? new DiffCell(text, foreBrush, FontWeights.Normal)
                : changed ? new DiffCell(text, foreBrush, FontWeights.SemiBold) : new DiffCell(text, quiet, FontWeights.Normal);

            return new DiffRow(c.KindText, c.App,
                Cell(c.Monitor, c.MonitorChanged), Cell(c.State, c.StateChanged), Cell(c.Position, c.PositionChanged),
                Cell(c.Size, c.SizeChanged), Cell(c.Note, c.NoteChanged),
                c.Kind == ChangeKind.Unchanged ? (Brush)Brush(Colors.Transparent) : Brush(back), foreBrush);
        }
    }
}
