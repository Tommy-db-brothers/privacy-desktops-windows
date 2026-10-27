using Microsoft.Win32;

namespace DeskVeil.Diagnostics;

/// <summary>Reads (never changes) the Explorer settings that decide whether other desktops' windows are visible.</summary>
internal static class SettingsAudit
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public static SettingsAuditReport Run()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
        var report = SettingsAuditReport.FromRegistryValues(
            key?.GetValue("VirtualDesktopTaskbarFilter"),
            key?.GetValue("VirtualDesktopAltTabFilter"));
        Log.Info($"Settings audit: taskbar all desktops={report.TaskbarShowsAllDesktops}, Alt+Tab all desktops={report.AltTabShowsAllDesktops}.");
        return report;
    }
}
