using DeskVeil.Config;
using DeskVeil.Desktops;
using DeskVeil.Diagnostics;
using DeskVeil.Guard;

namespace DeskVeil;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\DeskVeil.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("DeskVeil draait al (zie het systeemvak).", "DeskVeil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled exception on the UI thread", e.Exception);

        var store = ConfigStore.Load();
        Log.Info($"Config loaded from '{store.FilePath}' ({store.Config.Desktops.Count} desktop entries).");

        var problem = VirtualDesktopService.CheckAvailability();
        if (problem is not null)
        {
            MessageBox.Show(problem, "DeskVeil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        using var desktops = new VirtualDesktopService();
        using var curtain = new CurtainManager();
        using var guard = new GuardCoordinator(desktops, store, curtain, new HelloVerifier());
        using var tray = new TrayController(guard);

        guard.Start();
        if (store.LastBackupPath is not null)
        {
            tray.ShowBalloon($"Het configuratiebestand was beschadigd en is bewaard als '{store.LastBackupPath}'. DeskVeil start met lege instellingen.", ToolTipIcon.Warning);
        }
        else
        {
            tray.RunSettingsAudit(reportWhenOk: false);
        }

        Application.Run();
        return 0;
    }
}
