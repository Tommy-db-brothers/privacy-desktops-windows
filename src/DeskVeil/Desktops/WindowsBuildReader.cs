using Microsoft.Win32;

namespace DeskVeil.Desktops;

internal static class WindowsBuildReader
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static WindowsBuild Read()
    {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        var build = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out var b) ? b : Environment.OSVersion.Version.Build;
        var revision = key?.GetValue("UBR") is int ubr ? ubr : 0;
        return new WindowsBuild(build, revision, key?.GetValue("DisplayVersion") as string);
    }
}
