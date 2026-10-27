using DeskVeil.Config;
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
    }
}
