using System.Windows;
using AlexLV.WindowsUi;

namespace MonitorMover.Ui;

/// <summary>A one-line text prompt. OK refuses an empty answer with an inline error instead of closing.</summary>
public partial class PromptWindow : ThemedWindow
{
    private PromptWindow(string caption, string message, string initial)
    {
        InitializeComponent();
        Title = caption;
        Message.Text = message;
        Input.Text = initial;
        Input.TextChanged += (_, _) =>
        {
            if (Input.Text.Trim().Length == 0) return;
            Error.Visibility = Visibility.Collapsed;
            Input.ClearValue(StyleProperty);
        };
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    public string? Answer { get; private set; }

    /// <summary>The trimmed answer, or null when the user cancels.</summary>
    public static string? Ask(Window? owner, string caption, string message, string initial = "")
    {
        var prompt = new PromptWindow(caption, message, initial) { Owner = owner };
        if (owner is null) prompt.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return prompt.ShowDialog() == true ? prompt.Answer : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = Input.Text.Trim();
        if (text.Length == 0)
        {
            Error.Visibility = Visibility.Visible;
            Input.SetResourceReference(StyleProperty, "InvalidTextBox");
            Input.Focus();
            return;
        }

        Answer = text;
        DialogResult = true;
    }
}
