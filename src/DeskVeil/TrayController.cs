using System.Drawing.Drawing2D;
using DeskVeil.Config;
using DeskVeil.Diagnostics;
using DeskVeil.Guard;
using DeskVeil.Ui;

namespace DeskVeil;

/// <summary>Tray icon and its context menu. The menu is rebuilt each time it opens, so it always
/// reflects the current desktops, locks and settings.</summary>
internal sealed class TrayController : IDisposable
{
    private readonly GuardCoordinator _guard;
    private readonly ContextMenuStrip _menu = new();
    private readonly Icon _icon;
    private readonly NotifyIcon _notifyIcon;

    public TrayController(GuardCoordinator guard)
    {
        _guard = guard;
        _icon = CreateIcon();
        _menu.Opening += (_, _) => RebuildMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "DeskVeil",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _guard.Notification += OnNotification;
        _guard.VerifierSetupRequired += OnVerifierSetupRequired;
        RebuildMenu();
    }

    /// <summary>"DeskVeil 0.2.0" without the source revision suffix.</summary>
    public static string ProductTitle => $"DeskVeil {Application.ProductVersion.Split('+')[0]}";

    public void ShowBalloon(string text, ToolTipIcon icon) =>
        _notifyIcon.ShowBalloonTip(5000, "DeskVeil", text, icon);

    /// <summary>Warns when Windows shows other desktops' windows; optionally also confirms when it does not.</summary>
    public void RunSettingsAudit(bool reportWhenOk)
    {
        var report = SettingsAudit.Run();
        if (report.Leaks)
        {
            ShowBalloon(report.Describe(), ToolTipIcon.Warning);
        }
        else if (reportWhenOk)
        {
            ShowBalloon(report.Describe(), ToolTipIcon.Info);
        }
    }

    /// <summary>Adds the icon to the (new) taskbar again, after explorer.exe restarted.</summary>
    public void RestoreIcon()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        _guard.Notification -= OnNotification;
        _guard.VerifierSetupRequired -= OnVerifierSetupRequired;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private void OnNotification(object? sender, string message) => ShowBalloon(message, ToolTipIcon.Warning);

    private void OnVerifierSetupRequired(object? sender, VerifierStatus status) => Dialogs.ShowHelloSetup(status);

    private void RebuildMenu()
    {
        var oldItems = _menu.Items.Cast<ToolStripItem>().ToList();
        _menu.Items.Clear();
        foreach (var item in oldItems)
        {
            item.Dispose();
        }

        _menu.Items.Add(new ToolStripMenuItem(ProductTitle) { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());

        var current = _guard.CurrentDesktopId;
        foreach (var desktop in _guard.RefreshDesktops())
        {
            var locked = _guard.IsLocked(desktop.Id);
            var text = (locked ? "🔒 " : string.Empty) + desktop.Name + (desktop.Id == current ? "  (huidig)" : string.Empty);
            var id = desktop.Id;
            var item = new ToolStripMenuItem(text)
            {
                ToolTipText = locked ? "Klik om de vergrendeling op te heffen (Windows Hello)" : "Klik om te vergrendelen",
                Enabled = !_guard.IsVerifying,
            };
            item.Click += async (_, _) => await _guard.ToggleLockAsync(id);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());

        var lockCurrent = new ToolStripMenuItem("Huidige desktop locken")
        {
            Enabled = current is Guid c && !_guard.IsLocked(c) && !_guard.IsVerifying,
        };
        lockCurrent.Click += async (_, _) => await _guard.LockCurrentDesktopAsync();
        _menu.Items.Add(lockCurrent);

        var relock = new ToolStripMenuItem("Opnieuw vergrendelen bij verlaten") { Checked = _guard.RelockOnLeave };
        relock.Click += (_, _) => _guard.SetRelockOnLeave(!_guard.RelockOnLeave);
        _menu.Items.Add(relock);

        var audit = new ToolStripMenuItem("Instellingen-audit");
        audit.Click += (_, _) => RunSettingsAudit(reportWhenOk: true);
        _menu.Items.Add(audit);

        var autostart = new ToolStripMenuItem("Starten met Windows") { Checked = Autostart.IsEnabled() };
        autostart.Click += (_, _) => ToggleAutostart();
        _menu.Items.Add(autostart);

        var logs = new ToolStripMenuItem("Logmap openen");
        logs.Click += (_, _) => OpenLogFolder();
        _menu.Items.Add(logs);

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Afsluiten");
        exit.Click += (_, _) => Application.Exit();
        _menu.Items.Add(exit);
    }

    private static void OpenLogFolder()
    {
        var folder = Log.FileDirectory ?? Log.DefaultDirectory;
        Directory.CreateDirectory(folder);
        Dialogs.Open(folder);
    }

    private void ToggleAutostart()
    {
        try
        {
            Autostart.SetEnabled(!Autostart.IsEnabled());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error("Changing autostart failed", ex);
            ShowBalloon($"Automatisch starten kon niet worden aangepast: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var background = new SolidBrush(Color.FromArgb(37, 99, 235));
            graphics.FillEllipse(background, 0, 0, 31, 31);
            LockGlyph.Draw(graphics, new RectangleF(7, 6, 18, 19), Color.White, open: false);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
