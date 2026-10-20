using System.Windows.Forms;

namespace MonitorMover;

/// <summary>
/// The hidden top-level window setup and uninstall find by its title, MonitorMoverListener, and
/// send WM_CLOSE to for a clean exit.
/// </summary>
internal sealed class ShutdownListenerWindow : Form
{
    private const int WM_CLOSE = 0x0010;
    private const uint MSGFLT_ALLOW = 1;

    private readonly Action _onShutdown;

    public ShutdownListenerWindow(Action onShutdown)
    {
        _onShutdown = onShutdown;
        Text = "MonitorMoverListener";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;
        Size = System.Drawing.Size.Empty;
        Opacity = 0;

        // SetVisibleCore keeps the form hidden, so Show() alone never creates the window and setup
        // would find nothing. Create the (invisible) handle explicitly.
        CreateHandle();

        // Monitor Mover runs elevated and setup does not: let setup's WM_CLOSE through UIPI.
        NativeMethods.ChangeWindowMessageFilterEx(Handle, WM_CLOSE, MSGFLT_ALLOW, IntPtr.Zero);
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLOSE)
        {
            _onShutdown();
            return;
        }

        base.WndProc(ref m);
    }
}
