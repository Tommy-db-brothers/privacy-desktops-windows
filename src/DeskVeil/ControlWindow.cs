using System.Runtime.InteropServices;

namespace DeskVeil;

/// <summary>
/// Hidden top-level window with a fixed title, so other processes can find the running instance:
/// the installer posts WM_CLOSE to shut DeskVeil down cleanly, and a second DeskVeil.exe posts
/// <see cref="AnotherInstanceStarted"/>. Being top-level (not message-only) it also receives the
/// TaskbarCreated broadcast that follows an explorer.exe restart.
/// </summary>
internal sealed partial class ControlWindow : NativeWindow, IDisposable
{
    /// <summary>Also used by installer/DeskVeil.iss (FindWindowByWindowName).</summary>
    public const string Title = "DeskVeil.ControlWindow";

    private const int WM_CLOSE = 0x0010;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private static readonly int s_taskbarCreatedMessage = (int)RegisterWindowMessageW("TaskbarCreated");
    private static readonly int s_anotherInstanceMessage = (int)RegisterWindowMessageW("DeskVeil.AnotherInstanceStarted");

    public ControlWindow() =>
        CreateHandle(new CreateParams { Caption = Title, ExStyle = WS_EX_TOOLWINDOW });

    /// <summary>WM_CLOSE arrived, for example from the installer.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>explorer.exe (re)started and recreated the taskbar.</summary>
    public event EventHandler? ShellRestarted;

    public event EventHandler? AnotherInstanceStarted;

    /// <summary>Tells the running instance that another start was attempted.</summary>
    /// <returns>False when no running instance window was found.</returns>
    public static bool NotifyRunningInstance()
    {
        var hwnd = FindWindowW(null, Title);
        return hwnd != 0 && PostMessageW(hwnd, (uint)s_anotherInstanceMessage, 0, 0);
    }

    public void Dispose() => DestroyHandle();

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLOSE)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (s_taskbarCreatedMessage != 0 && m.Msg == s_taskbarCreatedMessage)
        {
            ShellRestarted?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (s_anotherInstanceMessage != 0 && m.Msg == s_anotherInstanceMessage)
        {
            AnotherInstanceStarted?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.WndProc(ref m);
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessageW(string message);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowW(string? className, string windowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint hwnd, uint message, nint wParam, nint lParam);
}
