namespace DeskVeil.Guard;

internal static class ForegroundHelper
{
    /// <summary>
    /// Moves a window to the foreground. A desktop switch is not input to DeskVeil, so Windows'
    /// foreground lock may refuse a plain SetForegroundWindow; attaching to the input queue of the
    /// current foreground thread is the documented way around that for a cooperating app.
    /// </summary>
    public static void BringToForeground(nint hwnd)
    {
        if (NativeMethods.GetForegroundWindow() == hwnd || NativeMethods.SetForegroundWindow(hwnd))
        {
            return;
        }

        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var ownThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ownThread)
        {
            NativeMethods.BringWindowToTop(hwnd);
            return;
        }

        if (NativeMethods.AttachThreadInput(ownThread, foregroundThread, true))
        {
            try
            {
                NativeMethods.BringWindowToTop(hwnd);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                NativeMethods.AttachThreadInput(ownThread, foregroundThread, false);
            }
        }
    }

    public static void RaiseTopmost(nint hwnd) =>
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
}
