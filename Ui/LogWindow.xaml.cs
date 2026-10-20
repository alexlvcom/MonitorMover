using System.Windows;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>The read-only "Apply Profile" result log: one OK, SKIP or FAIL line per window or rule.</summary>
public partial class LogWindow : ThemedWindow
{
    private LogWindow(string caption, IReadOnlyCollection<string> lines)
    {
        InitializeComponent();
        Title = caption;
        LogText.Text = string.Join(Environment.NewLine, lines);
        var ok = lines.Count(l => l.StartsWith("OK", StringComparison.Ordinal));
        Summary.Text = $"{ok} placed · {lines.Count - ok} skipped or failed";
        // Focus Close, so nothing in the log starts selected.
        Loaded += (_, _) => CloseButton.Focus();
    }

    public static void Show(Window? owner, string caption, IEnumerable<string> lines)
    {
        var window = new LogWindow(caption, lines.ToList()) { Owner = owner };
        if (owner is null) window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
