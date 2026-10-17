using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorMover;

public sealed class AboutForm : Form
{
    private readonly Func<Task> _onCheckForUpdates;
    private readonly Button _closeButton;

    public AboutForm(Func<Task> onCheckForUpdates)
    {
        _onCheckForUpdates = onCheckForUpdates;

        Text = $"About Monitor Mover (v{Changelog.MarketingVersion})";
        AppIcon.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(580, 520);

        // Header icon & titles
        var iconBox = new PictureBox
        {
            Location = new Point(20, 20),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.CenterImage,
        };
        if (AppIcon.Default != null)
        {
            iconBox.Image = new Icon(AppIcon.Default, new Size(48, 48)).ToBitmap();
        }

        var titleLabel = new Label
        {
            Text = $"Monitor Mover  v{Changelog.MarketingVersion}",
            Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(80, 18),
        };

        var buildDateStr = Changelog.BuildDate == DateTime.MinValue
            ? ""
            : $"   ·   built {Changelog.BuildDate:yyyy-MM-dd HH:mm}";
        var buildLabel = new Label
        {
            Text = $"Build {Changelog.BuildVersion}{buildDateStr}",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(80, 44),
        };

        var linkLabel = new LinkLabel
        {
            Text = "alexlvcom   ·   https://github.com/alexlvcom/MonitorMover",
            AutoSize = true,
            Location = new Point(80, 64),
        };
        linkLabel.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://github.com/alexlvcom/MonitorMover", UseShellExecute = true });
            }
            catch { }
        };

        // Diagnostics
        var exePath = Application.ExecutablePath;
        var profilesPath = ProfileStore.DefaultFilePath;
        var settingsPath = AppSettings.SettingsFilePath;

        int y = 96;
        var exeLabel = new Label { Text = "Executable:", AutoSize = true, Location = new Point(20, y + 3) };
        var exeText = new TextBox { Text = exePath, Location = new Point(110, y), Size = new Size(446, 24), ReadOnly = true, TabStop = false };
        y += 30;

        var profLabel = new Label { Text = "Profiles file:", AutoSize = true, Location = new Point(20, y + 3) };
        var profText = new TextBox { Text = profilesPath, Location = new Point(110, y), Size = new Size(446, 24), ReadOnly = true, TabStop = false };
        y += 30;

        var setLabel = new Label { Text = "Settings file:", AutoSize = true, Location = new Point(20, y + 3) };
        var setText = new TextBox { Text = settingsPath, Location = new Point(110, y), Size = new Size(446, 24), ReadOnly = true, TabStop = false };
        y += 34;

        // Release notes
        var notesHeader = new Label
        {
            Text = $"Release notes for v{Changelog.MarketingVersion}:",
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, y),
        };

        var fullHistoryButton = new LinkLabel
        {
            Text = "View full change history",
            AutoSize = true,
            Location = new Point(410, y),
        };

        y += 24;
        var notesText = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(536, 170),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9f),
            Text = Changelog.GetReleaseNotes(Changelog.MarketingVersion),
            TabStop = false,
        };

        fullHistoryButton.LinkClicked += (_, _) =>
        {
            ResultViewer.Show("Monitor Mover Change History", Changelog.FormatFull().Split('\n').Select(l => l.TrimEnd('\r')));
        };

        // Footer buttons
        _closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(466, 474),
            Size = new Size(90, 30),
            TabIndex = 0,
        };

        var copyDetailsButton = new Button
        {
            Text = "Copy details",
            Location = new Point(20, 474),
            Size = new Size(110, 30),
        };
        copyDetailsButton.Click += (_, _) =>
        {
            var details = $"Monitor Mover v{Changelog.MarketingVersion} (Build {Changelog.BuildVersion})\n" +
                          $"Built: {(Changelog.BuildDate == DateTime.MinValue ? "Unknown" : Changelog.BuildDate.ToString("yyyy-MM-dd HH:mm"))}\n" +
                          $"Executable: {exePath}\n" +
                          $"Profiles: {profilesPath}\n" +
                          $"Settings: {settingsPath}\n" +
                          $"Repository: https://github.com/alexlvcom/MonitorMover\n\n" +
                          $"Release notes (v{Changelog.MarketingVersion}):\n{notesText.Text}";
            Clipboard.SetText(details);
            MessageBox.Show(this, "Application details copied to clipboard.", "Monitor Mover", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var checkUpdatesButton = new Button
        {
            Text = "Check for updates",
            Location = new Point(138, 474),
            Size = new Size(140, 30),
        };
        checkUpdatesButton.Click += async (_, _) =>
        {
            checkUpdatesButton.Enabled = false;
            try
            {
                await _onCheckForUpdates();
            }
            finally
            {
                if (!checkUpdatesButton.IsDisposed) checkUpdatesButton.Enabled = true;
            }
        };

        Controls.AddRange(new Control[]
        {
            iconBox, titleLabel, buildLabel, linkLabel,
            exeLabel, exeText,
            profLabel, profText,
            setLabel, setText,
            notesHeader, fullHistoryButton, notesText,
            copyDetailsButton, checkUpdatesButton, _closeButton
        });

        AcceptButton = _closeButton;
        CancelButton = _closeButton;

        // Deselect text and give initial focus to Close button
        Shown += (_, _) =>
        {
            exeText.Select(0, 0);
            profText.Select(0, 0);
            setText.Select(0, 0);
            notesText.Select(0, 0);
            _closeButton.Focus();
        };
    }
}
