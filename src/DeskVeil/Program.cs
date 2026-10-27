using DeskVeil.Config;
using DeskVeil.Desktops;
using DeskVeil.Diagnostics;

namespace DeskVeil;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var store = ConfigStore.Load();
        Log.Info($"Config loaded from '{store.FilePath}' ({store.Config.Desktops.Count} desktop entries).");

        var problem = VirtualDesktopService.CheckAvailability();
        if (problem is not null)
        {
            MessageBox.Show(problem, "DeskVeil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var desktops = new VirtualDesktopService();
        foreach (var desktop in desktops.GetDesktops())
        {
            Log.Info($"Desktop #{desktop.Index} {desktop.Id} '{desktop.Name}'");
        }
        desktops.Start();
        Application.Run();
    }
}
