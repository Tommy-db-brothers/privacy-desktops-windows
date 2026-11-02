namespace DeskVeil.Desktops;

/// <summary>The VirtualDesktopAccessor functions DeskVeil uses, behind an interface so the service can be tested.</summary>
internal interface IVirtualDesktopApi
{
    int GetCurrentDesktopNumber();

    int GetDesktopCount();

    Guid GetDesktopIdByNumber(int number);

    int GoToDesktopNumber(int number);

    /// <summary>The custom name, "" for an unnamed desktop, or null when it cannot be read.</summary>
    string? GetDesktopName(int number);

    int RegisterPostMessageHook(nint listenerHwnd, uint messageId);

    void UnregisterPostMessageHook(nint listenerHwnd);
}

/// <summary>Source of the window messages the DLL posts after a desktop switch.</summary>
internal interface IDesktopSwitchListener : IDisposable
{
    nint Handle { get; }

    /// <summary>Raised with the (old, new) desktop numbers as reported by the DLL.</summary>
    event EventHandler<(int OldNumber, int NewNumber)>? DesktopSwitched;
}
