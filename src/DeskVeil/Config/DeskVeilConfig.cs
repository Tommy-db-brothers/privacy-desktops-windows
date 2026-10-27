using DeskVeil.Desktops;

namespace DeskVeil.Config;

public sealed class DeskVeilConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>When true, a desktop unlocked with Windows Hello locks again as soon as you leave it.</summary>
    public bool RelockOnLeave { get; set; } = true;

    /// <summary>Per-desktop settings, keyed by the desktop GUID (never by index or name, both of which shift).</summary>
    public Dictionary<Guid, DesktopEntry> Desktops { get; set; } = [];

    public bool IsLocked(Guid desktopId) =>
        Desktops.TryGetValue(desktopId, out var entry) && entry.Locked;

    /// <returns>True when the configuration changed.</returns>
    public bool SetLocked(Guid desktopId, bool locked, string? name = null)
    {
        if (!Desktops.TryGetValue(desktopId, out var entry))
        {
            entry = new DesktopEntry();
            Desktops[desktopId] = entry;
        }
        else if (entry.Locked == locked && (name is null || entry.LastKnownName == name))
        {
            return false;
        }

        entry.Locked = locked;
        if (name is not null)
        {
            entry.LastKnownName = name;
        }
        return true;
    }

    /// <summary>
    /// Brings the entries in line with the desktops that exist now: drops entries for removed desktops,
    /// adds (unlocked) entries for new ones and refreshes the remembered names.
    /// An empty list is treated as "could not enumerate" and changes nothing, so a transient
    /// failure never wipes the locks.
    /// </summary>
    /// <returns>True when the configuration changed.</returns>
    public bool Reconcile(IReadOnlyCollection<DesktopInfo> desktops)
    {
        if (desktops.Count == 0)
        {
            return false;
        }

        var changed = false;
        var present = desktops.Select(d => d.Id).ToHashSet();
        foreach (var stale in Desktops.Keys.Where(id => !present.Contains(id)).ToList())
        {
            Desktops.Remove(stale);
            changed = true;
        }

        foreach (var desktop in desktops)
        {
            if (!Desktops.TryGetValue(desktop.Id, out var entry))
            {
                Desktops[desktop.Id] = new DesktopEntry { LastKnownName = desktop.Name };
                changed = true;
            }
            else if (entry.LastKnownName != desktop.Name)
            {
                entry.LastKnownName = desktop.Name;
                changed = true;
            }
        }
        return changed;
    }
}

public sealed class DesktopEntry
{
    public bool Locked { get; set; }

    /// <summary>Informational only, so the JSON stays readable. Never used as a key.</summary>
    public string? LastKnownName { get; set; }
}
