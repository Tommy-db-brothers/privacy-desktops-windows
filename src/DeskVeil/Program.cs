using DeskVeil.Config;
using DeskVeil.Desktops;
using DeskVeil.Diagnostics;
using DeskVeil.Guard;
using DeskVeil.Ui;

namespace DeskVeil;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var instance = new SingleInstance();
        if (!instance.IsFirstInstance)
        {
            // The running instance shows "DeskVeil draait al". Retry briefly in case it is still starting.
            for (var attempt = 0; attempt < 10 && !ControlWindow.NotifyRunningInstance(); attempt++)
            {
                Thread.Sleep(200);
            }
            return 1;
        }

        Log.EnableFileLogging();
        try
        {
            return Run();
        }
        finally
        {
            Log.Info("DeskVeil stopped.");
            Log.DisableFileLogging();
        }
    }

    private static int Run()
    {
        var build = WindowsBuildReader.Read();
        Log.Info($"{TrayController.ProductTitle} starting on Windows {build}.");

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled exception on the UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

        var store = ConfigStore.Load();
        Log.Info($"Config loaded from '{store.FilePath}' ({store.Config.Desktops.Count} desktop entries).");

        var problem = VirtualDesktopService.CheckAvailability(build);
        if (problem is not null)
        {
            Dialogs.ShowUnsupported(problem, WindowsBuild.SupportedVersionsUrl);
            return 2;
        }

        var verifier = new HelloVerifier();
        using var desktops = new VirtualDesktopService();
        using var curtain = new CurtainManager();
        using var guard = new GuardCoordinator(desktops, store, curtain, verifier);
        using var tray = new TrayController(guard);
        using var control = new ControlWindow();
        control.CloseRequested += (_, _) => Application.Exit();
        control.AnotherInstanceStarted += (_, _) => tray.ShowBalloon("DeskVeil draait al.", ToolTipIcon.Info);
        control.ShellRestarted += (_, _) =>
        {
            tray.RestoreIcon();
            guard.HandleShellRestarted();
        };

        try
        {
            guard.Start();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error("Starting the guard failed", ex);
            Dialogs.ShowUnsupported($"DeskVeil kan de bureaublad-wissels niet volgen op Windows {build}: {ex.Message}", WindowsBuild.SupportedVersionsUrl);
            return 3;
        }

        if (store.LastBackupPath is not null)
        {
            tray.ShowBalloon($"Het configuratiebestand was beschadigd en is bewaard als '{store.LastBackupPath}'. DeskVeil start met lege instellingen.", ToolTipIcon.Warning);
        }

        if (!store.Config.FirstRunCompleted)
        {
            ShowWelcome(store, verifier);
        }
        else
        {
            tray.RunSettingsAudit(reportWhenOk: false);
        }

        Application.Run();
        return 0;
    }

    private static void ShowWelcome(ConfigStore store, IVerifier verifier)
    {
        store.Config.FirstRunCompleted = true;
        try
        {
            store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Saving the first-run flag failed", ex);
        }

        var welcome = new WelcomeWindow(SettingsAudit.Run(), verifier.CheckAvailabilityAsync());
        welcome.FormClosed += (_, _) => welcome.Dispose();
        welcome.Show();
    }
}
