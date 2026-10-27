using System.Runtime.InteropServices;

namespace DeskVeil.Desktops;

/// <summary>
/// P/Invoke surface of Ciantic's VirtualDesktopAccessor.dll (see lib/README.md).
/// Signatures follow dll/src/lib.rs of the pinned release. Desktop numbers are 0-based;
/// functions return -1 on error and GUID-returning functions return <see cref="Guid.Empty"/>.
/// </summary>
internal static partial class VirtualDesktopAccessor
{
    public const string DllName = "VirtualDesktopAccessor.dll";

    [LibraryImport(DllName)]
    public static partial int GetCurrentDesktopNumber();

    [LibraryImport(DllName)]
    public static partial int GetDesktopCount();

    [LibraryImport(DllName)]
    public static partial Guid GetDesktopIdByNumber(int number);

    [LibraryImport(DllName)]
    public static partial int GoToDesktopNumber(int desktopNumber);

    /// <summary>Writes the NUL-terminated UTF-8 name into the buffer. Returns 1 on success, 0 when the
    /// name could not be read and -1 when the buffer is too small. Unnamed desktops yield "".</summary>
    [LibraryImport(DllName)]
    public static partial int GetDesktopName(int desktopNumber, [Out] byte[] utf8Buffer, nuint bufferLength);

    /// <summary>Returns 1 if the window is on the current desktop, 0 if not, -1 if unknown
    /// (for example for tool windows, which Windows shows on every desktop).</summary>
    [LibraryImport(DllName)]
    public static partial int IsWindowOnCurrentVirtualDesktop(nint hwnd);

    /// <summary>After a desktop switch the DLL posts <paramref name="messageId"/> to the window with
    /// wParam = old desktop number and lParam = new desktop number. Returns 1 on success.</summary>
    [LibraryImport(DllName)]
    public static partial int RegisterPostMessageHook(nint listenerHwnd, uint messageId);

    [LibraryImport(DllName)]
    public static partial void UnregisterPostMessageHook(nint listenerHwnd);
}
