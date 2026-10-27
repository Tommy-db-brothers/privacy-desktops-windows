using System.Text;
using DeskVeil.Diagnostics;

namespace DeskVeil.Desktops;

/// <summary><see cref="IDesktopService"/> backed by VirtualDesktopAccessor.dll. Must be created and used on the UI thread.</summary>
public sealed class VirtualDesktopService : IDesktopService
{
    // WM_APP + 0x2D1; WM_APP..0xBFFF is reserved for private application messages.
    private const int DesktopSwitchedMessage = 0x8000 + 0x2D1;

    private DesktopSwitchListener? _listener;
    private Guid? _lastKnownCurrent;

    public event EventHandler<DesktopChangedEventArgs>? CurrentDesktopChanged;

    /// <summary>Checks that the DLL loads and talks to the shell.</summary>
    /// <returns>Null when usable, otherwise a message for the user.</returns>
    public static string? CheckAvailability()
    {
        try
        {
            return VirtualDesktopAccessor.GetDesktopCount() > 0
                ? null
                : "VirtualDesktopAccessor.dll kon de virtuele bureaubladen niet uitlezen. Wordt deze Windows-build ondersteund (Windows 11 24H2 of nieuwer)?";
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Log.Error("VirtualDesktopAccessor.dll could not be loaded", ex);
            return $"VirtualDesktopAccessor.dll kon niet geladen worden ({ex.GetType().Name}). Staat de x64-DLL naast DeskVeil.exe?";
        }
    }

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _listener = new DesktopSwitchListener(DesktopSwitchedMessage);
        _listener.DesktopSwitched += OnDesktopSwitched;
        if (VirtualDesktopAccessor.RegisterPostMessageHook(_listener.Handle, DesktopSwitchedMessage) != 1)
        {
            _listener.Dispose();
            _listener = null;
            throw new InvalidOperationException("RegisterPostMessageHook failed.");
        }

        _lastKnownCurrent = GetCurrentDesktopId();
        Log.Info($"Listening for desktop switches; current desktop {_lastKnownCurrent?.ToString() ?? "unknown"}.");
    }

    public IReadOnlyList<DesktopInfo> GetDesktops()
    {
        var count = VirtualDesktopAccessor.GetDesktopCount();
        if (count <= 0)
        {
            return [];
        }

        var desktops = new List<DesktopInfo>(count);
        var buffer = new byte[1024];
        for (var number = 0; number < count; number++)
        {
            var id = VirtualDesktopAccessor.GetDesktopIdByNumber(number);
            if (id == Guid.Empty)
            {
                // The set of desktops changed while enumerating; report "unknown" rather than a partial list.
                return [];
            }
            desktops.Add(new DesktopInfo(id, number, ReadName(number, buffer)));
        }
        return desktops;
    }

    public Guid? GetCurrentDesktopId()
    {
        var number = VirtualDesktopAccessor.GetCurrentDesktopNumber();
        if (number < 0)
        {
            return null;
        }
        var id = VirtualDesktopAccessor.GetDesktopIdByNumber(number);
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
        return VirtualDesktopAccessor.GoToDesktopNumber(target.Index) == 1;
    }

    public void Dispose()
    {
        if (_listener is null)
        {
            return;
        }
        VirtualDesktopAccessor.UnregisterPostMessageHook(_listener.Handle);
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

    private static string ReadName(int number, byte[] buffer)
    {
        Array.Clear(buffer);
        var name = VirtualDesktopAccessor.GetDesktopName(number, buffer, (nuint)buffer.Length) == 1
            ? Encoding.UTF8.GetString(buffer, 0, Math.Max(0, Array.IndexOf(buffer, (byte)0)))
            : string.Empty;
        return string.IsNullOrWhiteSpace(name) ? $"Bureaublad {number + 1}" : name;
    }
}
