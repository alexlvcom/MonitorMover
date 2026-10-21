using System.IO;
using System.Windows;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>
/// Highlights acknowledged locally only after rendering: the Main changes of every release since <c>previous</c>, newest first,
/// or only the current release on a fresh installation.
/// </summary>
public partial class WhatsNewWindow : ThemedWindow
{
    public WhatsNewWindow(Version? previous = null)
    {
        InitializeComponent();
        Title = $"{AppInfo.DisplayName} What's New";
        TitleVersion = $"v{Changelog.MarketingVersion}";
        Heading.Text = $"What’s new in {AppInfo.DisplayName} {Changelog.MarketingVersion}";
        if (previous is not null) Subtitle.Text = $"Here’s what changed since v{previous.ToString(3)}.";
        var releases = Changelog.MainChangesSince(Changelog.Entries, previous, Changelog.MarketingVersion);
        if (releases.Length == 0) ReleaseNotesView.Fill(Changes, null);
        // Each release under its own version heading once there is a previous version to compare with.
        foreach (var release in releases) ReleaseNotesView.Fill(Changes, release, withVersionHeading: previous is not null);
        Loaded += (_, _) => ContinueButton.Focus();
    }

    public static void Schedule()
    {
        EventHandler? onIdle = null;
        onIdle = (_, _) =>
        {
            System.Windows.Forms.Application.Idle -= onIdle;
            ShowForCurrentRelease();
        };
        System.Windows.Forms.Application.Idle += onIdle;
    }

    public static void ShowForCurrentRelease(Window? owner = null)
    {
        var state = new WhatsNewState();
        if (!state.ShouldShow(Changelog.MarketingVersion)) return;
        try
        {
            var window = new WhatsNewWindow(state.PreviousRelease()) { Owner = owner };
            var recorded = false;
            window.ContentRendered += (_, _) =>
            {
                if (recorded) return;
                recorded = true;
                try { state.MarkShown(Changelog.MarketingVersion); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { System.Diagnostics.Debug.WriteLine(ex.Message); }
            };
            WinFormsHosting.ShowModeless(window);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Could not show release notes: {ex.Message}"); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void History_Click(object sender, RoutedEventArgs e) => new HistoryWindow { Owner = this }.ShowDialog();
}
