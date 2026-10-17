using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MonitorMover;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Func<Task> _onCheckForUpdates;
    private readonly ComboBox _frequencyCombo;

    public SettingsForm(AppSettings settings, Func<Task> onCheckForUpdates)
    {
        _settings = settings;
        _onCheckForUpdates = onCheckForUpdates;

        Text = $"Monitor Mover Settings (v{Changelog.MarketingVersion})";
        AppIcon.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(540, 270);

        var toolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 10000, ShowAlways = true };

        // Updates Group
        var updatesGroup = new GroupBox
        {
            Text = "Updates",
            Location = new Point(16, 16),
            Size = new Size(508, 100),
        };

        var frequencyLabel = new Label
        {
            Text = "Check for updates:",
            AutoSize = true,
            Location = new Point(16, 32),
        };

        _frequencyCombo = new ComboBox
        {
            Location = new Point(140, 28),
            Size = new Size(200, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _frequencyCombo.Items.AddRange(new object[]
        {
            "Once an hour",
            "Once a day (default)",
            "Once a week",
            "Once a month",
            "Never"
        });

        _frequencyCombo.SelectedIndex = _settings.UpdateCheckFrequency switch
        {
            UpdateCheckFrequency.Hourly => 0,
            UpdateCheckFrequency.Daily => 1,
            UpdateCheckFrequency.Weekly => 2,
            UpdateCheckFrequency.Monthly => 3,
            UpdateCheckFrequency.Never => 4,
            _ => 1
        };

        toolTip.SetToolTip(_frequencyCombo, "Check at startup, then at this interval. Never disables automatic checks.");

        var checkNowButton = new Button
        {
            Text = "Check for updates",
            Location = new Point(350, 27),
            Size = new Size(140, 28),
        };
        checkNowButton.Click += async (_, _) =>
        {
            checkNowButton.Enabled = false;
            try
            {
                await _onCheckForUpdates();
            }
            finally
            {
                if (!checkNowButton.IsDisposed) checkNowButton.Enabled = true;
            }
        };

        var noteLabel = new Label
        {
            Text = "Checks GitHub releases asynchronously. Never disables automatic checks, but manual check remains available.",
            ForeColor = Color.Gray,
            AutoSize = false,
            Location = new Point(16, 62),
            Size = new Size(474, 30),
        };

        updatesGroup.Controls.AddRange(new Control[] { frequencyLabel, _frequencyCombo, checkNowButton, noteLabel });

        // Profiles Group
        var profilesGroup = new GroupBox
        {
            Text = "Profiles",
            Location = new Point(16, 126),
            Size = new Size(508, 80),
        };

        var profilesFolderLabel = new Label
        {
            Text = "Profiles folder:",
            AutoSize = true,
            Location = new Point(16, 32),
        };

        var profilesDir = ProfileStore.DefaultDirectory;
        var profilesFolderText = new TextBox
        {
            Text = profilesDir,
            Location = new Point(140, 28),
            Size = new Size(240, 26),
            ReadOnly = true,
        };

        var openFolderButton = new Button
        {
            Text = "Open folder",
            Location = new Point(390, 27),
            Size = new Size(100, 28),
        };
        openFolderButton.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(profilesDir);
                Process.Start(new ProcessStartInfo { FileName = profilesDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open the profiles folder.\n\n{ex.Message}", "Monitor Mover", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        profilesGroup.Controls.AddRange(new Control[] { profilesFolderLabel, profilesFolderText, openFolderButton });

        // Footer buttons
        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(334, 222),
            Size = new Size(90, 30),
        };
        saveButton.Click += (_, _) =>
        {
            _settings.UpdateCheckFrequency = _frequencyCombo.SelectedIndex switch
            {
                0 => UpdateCheckFrequency.Hourly,
                1 => UpdateCheckFrequency.Daily,
                2 => UpdateCheckFrequency.Weekly,
                3 => UpdateCheckFrequency.Monthly,
                4 => UpdateCheckFrequency.Never,
                _ => UpdateCheckFrequency.Daily
            };
            _settings.Save();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(434, 222),
            Size = new Size(90, 30),
        };

        Controls.AddRange(new Control[] { updatesGroup, profilesGroup, saveButton, cancelButton });
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }
}
