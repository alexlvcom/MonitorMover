using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>
/// Settings: General, Updates, Profiles, About and Diagnostics tabs over one draft copy. Nothing is
/// stored until Save; Cancel, Esc or the title-bar X discards the draft. The theme is the one live
/// preview: it applies at once and is restored on any close without a successful Save.
/// </summary>
public partial class SettingsWindow : ThemedWindow
{
    private static readonly UpdateCheckFrequency[] Frequencies =
    {
        UpdateCheckFrequency.Hourly, UpdateCheckFrequency.Daily, UpdateCheckFrequency.Weekly,
        UpdateCheckFrequency.Monthly, UpdateCheckFrequency.Never,
    };

    private readonly AppSettings _draft;
    private readonly AppSettings _original;
    private readonly Func<AppSettings, string?> _onSave;
    private readonly Func<Task<UpdateStatus>> _onCheckForUpdates;
    private readonly Action _onInstallUpdate;
    private bool _saved;
    private bool _loading = true;

    /// <param name="updateStatus">The last check's remembered result, so the card never claims "latest" after a failure.</param>
    /// <param name="onInstallUpdate">Opens the Update dialog for the release the last check found.</param>
    public SettingsWindow(AppSettings draft, UpdateStatus updateStatus, Func<AppSettings, string?> onSave,
        Func<Task<UpdateStatus>> onCheckForUpdates, Action onInstallUpdate)
    {
        InitializeComponent();
        _draft = draft;
        _original = draft.Clone();
        _onSave = onSave;
        _onCheckForUpdates = onCheckForUpdates;
        _onInstallUpdate = onInstallUpdate;

        // The version sits at the right of the title row; clicking it opens Updates.
        Title = $"{AppInfo.DisplayName} Settings";
        TitleVersion = $"v{Changelog.MarketingVersion}";
        VersionClick += (_, _) => Tabs.SelectedItem = UpdatesTab;
        Frequency.SelectedIndex = Math.Max(0, Array.IndexOf(Frequencies, UpdateSchedule.Normalize(draft.UpdateCheckFrequency)));
        (draft.Theme switch { AppTheme.Light => ThemeLight, AppTheme.Dark => ThemeDark, _ => ThemeSystem }).IsChecked = true;
        Frequency.SelectionChanged += (_, _) => OnEdited();
        ProfilesFolder.Text = ProfileStore.DefaultDirectory;

        ShowUpdateStatus(updateStatus);
        BuildAbout();
        BuildDiagnostics();
        _loading = false;
        OnEdited();

        Loaded += (_, _) => SaveButton.Focus();
        Closing += OnClosing;
    }

    public void SelectTab(string name)
    {
        foreach (TabItem tab in Tabs.Items)
        {
            if (string.Equals(tab.Header as string, name, StringComparison.OrdinalIgnoreCase)) Tabs.SelectedItem = tab;
        }
    }

    private AppTheme SelectedTheme =>
        ThemeLight.IsChecked == true ? AppTheme.Light : ThemeDark.IsChecked == true ? AppTheme.Dark : AppTheme.System;

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ThemeManager.Apply(SelectedTheme);
        OnEdited();
    }

    /// <summary>Copies the controls into the draft and refreshes the change count.</summary>
    private void OnEdited()
    {
        if (_loading) return;
        _draft.UpdateCheckFrequency = Frequencies[Math.Max(0, Frequency.SelectedIndex)];
        _draft.Theme = SelectedTheme;

        var changes = 0;
        if (_draft.UpdateCheckFrequency != UpdateSchedule.Normalize(_original.UpdateCheckFrequency)) changes++;
        if (_draft.Theme != _original.Theme) changes++;
        DirtyText.Text = changes switch { 0 => "", 1 => "1 unsaved change", _ => $"{changes} unsaved changes" };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        OnEdited();
        var error = _onSave(_draft);
        if (error is not null)
        {
            MessageDialog.Show($"Could not save settings to:\n{AppSettings.SettingsFilePath}\n\n{error}\n\nYour changes are still open in this window.",
                MessageKind.Error, owner: this);
            return;
        }

        _saved = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Any close without a successful Save restores the saved theme.
        if (!_saved && ThemeManager.Choice != _original.Theme) ThemeManager.Apply(_original.Theme);
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        ShowUpdateStatus(new UpdateStatus(UpdateState.Checking, "Checking for updates…", "Contacting GitHub releases"));
        try
        {
            ShowUpdateStatus(await _onCheckForUpdates());
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e) => _onInstallUpdate();

    private void ShowUpdateStatus(UpdateStatus status)
    {
        UpdateTitle.Text = status.Title;
        UpdateDetail.Text = status.Detail;
        UpdateNowButton.Visibility = status.State == UpdateState.Available ? Visibility.Visible : Visibility.Collapsed;
        UpdateDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, status.State switch
        {
            UpdateState.Failed => "Danger",
            UpdateState.Available or UpdateState.Checking => "Accent",
            UpdateState.NotChecked => "Muted",
            _ => "Ok",
        });
    }

    private void BuildAbout()
    {
        AboutIcon.Source = ThemeManager.WindowIcon;
        AboutTitle.Text = $"{AppInfo.DisplayName} v{Changelog.MarketingVersion}";
        AboutBuild.Text = Changelog.BuildDate == DateTime.MinValue
            ? $"Build {Changelog.BuildVersion} · by alexlvcom · "
            : $"Build {Changelog.BuildVersion} · built {Changelog.BuildDate:yyyy-MM-dd HH:mm} · by alexlvcom · ";
        NotesHeader.Text = $"Release notes for v{Changelog.MarketingVersion}:";
        ReleaseNotesView.Fill(NotesPanel, Changelog.Current);
    }

    private static string ExecutablePath => Environment.ProcessPath ?? "";

    private void BuildDiagnostics()
    {
        var rows = new (string Label, string Path)[]
        {
            ("Executable", ExecutablePath),
            ("Profiles file", ProfileStore.DefaultFilePath),
            ("Settings file", AppSettings.SettingsFilePath),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var (label, path) = rows[i];
            var row = new Border();
            row.SetResourceReference(StyleProperty, i == 0 ? "FirstCardRow" : "CardRow");
            var stack = new StackPanel();
            var caption = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) };
            caption.SetResourceReference(StyleProperty, "MutedText");
            stack.Children.Add(caption);

            var line = new DockPanel();
            var open = new Button { Content = "Open", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            open.Click += (_, _) => OpenFolder(Path.GetDirectoryName(path) ?? path, this);
            DockPanel.SetDock(open, Dock.Right);
            line.Children.Add(open);
            var box = new TextBox { Text = path };
            box.SetResourceReference(StyleProperty, "PathBox");
            line.Children.Add(box);
            stack.Children.Add(line);
            row.Child = stack;
            PathsPanel.Children.Add(row);
        }
    }

    private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(ProfileStore.DefaultDirectory, this);

    private void Repo_Click(object sender, RoutedEventArgs e) => OpenUrl(Changelog.RepositoryUrl);

    private void History_Click(object sender, RoutedEventArgs e) => new HistoryWindow { Owner = this }.ShowDialog();

    public static string Details() =>
        $"{AppInfo.DisplayName} v{Changelog.MarketingVersion} (Build {Changelog.BuildVersion})\n" +
        $"Built: {(Changelog.BuildDate == DateTime.MinValue ? "Unknown" : Changelog.BuildDate.ToString("yyyy-MM-dd HH:mm"))}\n" +
        $"Executable: {ExecutablePath}\n" +
        $"Profiles: {ProfileStore.DefaultFilePath}\n" +
        $"Settings: {AppSettings.SettingsFilePath}\n" +
        $"Theme: {ThemeManager.Choice} ({(ThemeManager.IsDark ? "dark" : "light")})\n" +
        $"Repository: {Changelog.RepositoryUrl}\n\n" +
        $"Release notes (v{Changelog.MarketingVersion}):\n{Changelog.GetReleaseNotes(Changelog.MarketingVersion)}";

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Details());
            MessageDialog.Show("Application details copied to clipboard.", MessageKind.Success, owner: this);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            MessageDialog.Show($"Could not copy to the clipboard.\n\n{ex.Message}", MessageKind.Warning, owner: this);
        }
    }

    internal static void OpenFolder(string path, Window? owner)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageDialog.Show($"Could not open the folder.\n\n{ex.Message}", MessageKind.Warning, owner: owner);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageDialog.Show($"Could not open {url}.\n\n{ex.Message}", MessageKind.Warning);
        }
    }
}
