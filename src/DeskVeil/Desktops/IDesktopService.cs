namespace DeskVeil.Desktops;

public interface IDesktopService : IDisposable
{
    /// <summary>Raised on the UI thread after the active virtual desktop changed, whatever caused the switch.</summary>
    event EventHandler<DesktopChangedEventArgs>? CurrentDesktopChanged;

    /// <summary>Starts listening for desktop switches.</summary>
    void Start();

    /// <summary>All desktops in Task View order, or an empty list if they could not be enumerated.</summary>
    IReadOnlyList<DesktopInfo> GetDesktops();

    /// <summary>The active desktop, or null when it cannot be determined (for example while a desktop is being removed).</summary>
    Guid? GetCurrentDesktopId();

    /// <returns>True when the switch was issued.</returns>
    bool SwitchTo(Guid desktopId);
}

public sealed class DesktopChangedEventArgs(Guid? previousId, Guid? currentId) : EventArgs
{
    /// <summary>The desktop that was active before this event, as last observed by the service.</summary>
    public Guid? PreviousId { get; } = previousId;

    /// <summary>The desktop that is active now, or null if it could not be read.</summary>
    public Guid? CurrentId { get; } = currentId;
}
