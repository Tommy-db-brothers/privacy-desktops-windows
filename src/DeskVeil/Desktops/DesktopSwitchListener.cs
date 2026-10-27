namespace DeskVeil.Desktops;

/// <summary>Message-only window that receives the desktop-switch messages posted by VirtualDesktopAccessor.</summary>
internal sealed class DesktopSwitchListener : NativeWindow, IDesktopSwitchListener
{
    private static readonly nint HWND_MESSAGE = -3;

    private readonly int _messageId;

    public DesktopSwitchListener(int messageId)
    {
        _messageId = messageId;
        CreateHandle(new CreateParams { Caption = "DeskVeil.DesktopSwitchListener", Parent = HWND_MESSAGE });
    }

    public event EventHandler<(int OldNumber, int NewNumber)>? DesktopSwitched;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == _messageId)
        {
            DesktopSwitched?.Invoke(this, ((int)m.WParam, (int)m.LParam));
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
