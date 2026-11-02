using DeskVeil.Diagnostics;

namespace DeskVeil.Desktops;

/// <summary><see cref="IDesktopService"/> backed by VirtualDesktopAccessor.dll. Must be created and used on the UI thread.</summary>
public sealed class VirtualDesktopService : IDesktopService
{
    // WM_APP + 0x2D1; WM_APP..0xBFFF is reserved for private application messages.
    internal const int DesktopSwitchedMessage = 0x8000 + 0x2D1;

    private readonly IVirtualDesktopApi _api;
    private readonly Func<int, IDesktopSwitchListener> _createListener;
    private IDesktopSwitchListener? _listener;
    private Guid? _lastKnownCurrent;

    public VirtualDesktopService()
        : this(new NativeVirtualDesktopApi(), messageId => new DesktopSwitchListener(messageId))
    {
    }

    internal VirtualDesktopService(IVirtualDesktopApi api, Func<int, IDesktopSwitchListener> createListener)
    {
        _api = api;
        _createListener = createListener;
    }

    public event EventHandler<DesktopChangedEventArgs>? CurrentDesktopChanged;

    /// <summary>
    /// Checks the Windows build first (an unsupported build can crash inside the DLL instead of
    /// returning an error) and then that the DLL loads and can read the desktops.
    /// </summary>
    /// <returns>Null when usable, otherwise a message for the user.</returns>
    public static string? CheckAvailability(WindowsBuild build) =>
        CheckAvailability(build, () => VirtualDesktopAccessor.GetDesktopCount());

    internal static string? CheckAvailability(WindowsBuild build, Func<int> getDesktopCount)
    {
        if (!build.IsSupported)
        {
            Log.Warn($"Unsupported Windows build {build}.");
            return $"DeskVeil werkt niet op deze Windows-versie: {build}.\n\n" +
                   $"Nodig is Windows 11 24H2 (build {WindowsBuild.MinimumBuild}.{WindowsBuild.MinimumRevision}) of nieuwer. " +
                   "Installeer de laatste Windows-updates en probeer het opnieuw.";
        }

        try
        {
            return getDesktopCount() > 0
                ? null
                : $"VirtualDesktopAccessor.dll kan de virtuele bureaubladen niet uitlezen op {build}. " +
                  "Mogelijk heeft een Windows-update de interne interfaces gewijzigd; kijk of er een nieuwere DeskVeil is.";
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Log.Error("VirtualDesktopAccessor.dll could not be loaded", ex);
            return $"VirtualDesktopAccessor.dll kon niet geladen worden ({ex.GetType().Name}) op {build}. " +
                   "Installeer DeskVeil opnieuw; de 64-bits DLL hoort naast DeskVeil.exe te staan.";
        }
    }

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        var listener = _createListener(DesktopSwitchedMessage);
        if (_api.RegisterPostMessageHook(listener.Handle, DesktopSwitchedMessage) != 1)
        {
            listener.Dispose();
            throw new InvalidOperationException("RegisterPostMessageHook failed.");
        }

        listener.DesktopSwitched += OnDesktopSwitched;
        _listener = listener;
        _lastKnownCurrent = GetCurrentDesktopId();
        Log.Info($"Listening for desktop switches; current desktop {_lastKnownCurrent?.ToString() ?? "unknown"}.");
    }

    public void Reconnect()
    {
        if (_listener is null)
        {
            Start();
            return;
        }

        // The DLL keeps one notification registration for all listeners; removing the only listener
        // tears it down and registering again builds a fresh one against the new shell.
        _api.UnregisterPostMessageHook(_listener.Handle);
        if (_api.RegisterPostMessageHook(_listener.Handle, DesktopSwitchedMessage) != 1)
        {
            throw new InvalidOperationException("RegisterPostMessageHook failed after the shell restarted.");
        }

        _lastKnownCurrent = GetCurrentDesktopId();
        Log.Info($"Desktop switch listener registered again; current desktop {_lastKnownCurrent?.ToString() ?? "unknown"}.");
    }

    public IReadOnlyList<DesktopInfo> GetDesktops()
    {
        var count = _api.GetDesktopCount();
        if (count <= 0)
        {
            return [];
        }

        var desktops = new List<DesktopInfo>(count);
        for (var number = 0; number < count; number++)
        {
            var id = _api.GetDesktopIdByNumber(number);
            if (id == Guid.Empty)
            {
                // The set of desktops changed while enumerating; report "unknown" rather than a partial list.
                return [];
            }
            var name = _api.GetDesktopName(number);
            desktops.Add(new DesktopInfo(id, number, string.IsNullOrWhiteSpace(name) ? $"Bureaublad {number + 1}" : name));
        }
        return desktops;
    }

    public Guid? GetCurrentDesktopId()
    {
        var number = _api.GetCurrentDesktopNumber();
        if (number < 0)
        {
            return null;
        }
        var id = _api.GetDesktopIdByNumber(number);
        return id == Guid.Empty ? null : id;
    }

    public bool SwitchTo(Guid desktopId)
    {
        var target = GetDesktops().FirstOrDefault(d => d.Id == desktopId);
        if (target is null)
        {
            Log.Warn($"Cannot switch to desktop {desktopId}: it no longer exists.");
            return false;
        }
        return _api.GoToDesktopNumber(target.Index) == 1;
    }

    public void Dispose()
    {
        if (_listener is null)
        {
            return;
        }
        _api.UnregisterPostMessageHook(_listener.Handle);
        _listener.DesktopSwitched -= OnDesktopSwitched;
        _listener.Dispose();
        _listener = null;
    }

    private void OnDesktopSwitched(object? sender, (int OldNumber, int NewNumber) e)
    {
        // The numbers in the message may already be stale (rapid switching, a removed desktop),
        // so the GUID of the current desktop is read back instead of trusting them.
        var current = GetCurrentDesktopId();
        var previous = _lastKnownCurrent;
        if (current is not null)
        {
            _lastKnownCurrent = current;
        }

        Log.Info($"Desktop switched: #{e.OldNumber} -> #{e.NewNumber}, {previous?.ToString() ?? "?"} -> {current?.ToString() ?? "?"}.");
        CurrentDesktopChanged?.Invoke(this, new DesktopChangedEventArgs(previous, current));
    }
}
