namespace DeskVeil.Diagnostics;

/// <summary>
/// Whether Windows itself shows windows from other desktops, which would leak the contents of
/// locked desktops regardless of the curtain. Interprets the DWORDs under
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced:
/// <c>VirtualDesktopTaskbarFilter</c> and <c>VirtualDesktopAltTabFilter</c>
/// (1 = only the current desktop, 0 = all desktops, absent = Windows 11 default: only the current desktop).
/// </summary>
public sealed record SettingsAuditReport(bool TaskbarShowsAllDesktops, bool AltTabShowsAllDesktops)
{
    public bool Leaks => TaskbarShowsAllDesktops || AltTabShowsAllDesktops;

    public static SettingsAuditReport FromRegistryValues(object? taskbarFilter, object? altTabFilter) =>
        new(ShowsAllDesktops(taskbarFilter), ShowsAllDesktops(altTabFilter));

    public string Describe()
    {
        if (!Leaks)
        {
            return "Taakbalk en Alt+Tab tonen alleen vensters van het huidige bureaublad.";
        }

        var leaks = new List<string>();
        if (TaskbarShowsAllDesktops)
        {
            leaks.Add("de taakbalk");
        }
        if (AltTabShowsAllDesktops)
        {
            leaks.Add("Alt+Tab");
        }
        return $"Let op: {string.Join(" en ", leaks)} toont vensters van alle bureaubladen, ook vergrendelde. " +
               "Zet in Instellingen > Systeem > Multitasking > Bureaubladen beide opties op 'Alleen op het bureaublad dat ik gebruik'.";
    }

    private static bool ShowsAllDesktops(object? value) => value is int dword && dword == 0;
}
