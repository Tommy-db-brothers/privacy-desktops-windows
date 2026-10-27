using System.Text;

namespace DeskVeil.Desktops;

internal sealed class NativeVirtualDesktopApi : IVirtualDesktopApi
{
    private readonly byte[] _nameBuffer = new byte[1024];

    public int GetCurrentDesktopNumber() => VirtualDesktopAccessor.GetCurrentDesktopNumber();

    public int GetDesktopCount() => VirtualDesktopAccessor.GetDesktopCount();

    public Guid GetDesktopIdByNumber(int number) => VirtualDesktopAccessor.GetDesktopIdByNumber(number);

    public int GoToDesktopNumber(int number) => VirtualDesktopAccessor.GoToDesktopNumber(number);

    public string? GetDesktopName(int number)
    {
        Array.Clear(_nameBuffer);
        if (VirtualDesktopAccessor.GetDesktopName(number, _nameBuffer, (nuint)_nameBuffer.Length) != 1)
        {
            return null;
        }
        return Encoding.UTF8.GetString(_nameBuffer, 0, Math.Max(0, Array.IndexOf(_nameBuffer, (byte)0)));
    }

    public int RegisterPostMessageHook(nint listenerHwnd, uint messageId) =>
        VirtualDesktopAccessor.RegisterPostMessageHook(listenerHwnd, messageId);

    public void UnregisterPostMessageHook(nint listenerHwnd) =>
        VirtualDesktopAccessor.UnregisterPostMessageHook(listenerHwnd);
}
