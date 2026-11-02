using DeskVeil.Diagnostics;
using DeskVeil.Guard;

namespace DeskVeil.Ui;

/// <summary>Shown once, on the very first start: how to use DeskVeil, what it is not, and whether
/// Windows itself leaks other desktops' windows or lacks Windows Hello.</summary>
internal sealed class WelcomeWindow : Form
{
    private readonly TableLayoutPanel _layout;
    private readonly Label _helloStatus;
    private readonly Button _openSignInOptions;

    public WelcomeWindow(SettingsAuditReport audit, Task<VerifierStatus> helloStatus)
    {
        Text = "Welkom bij DeskVeil";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24);
        LightTheme.Apply(this);

        _layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            BackColor = LightTheme.Background,
        };
        Controls.Add(_layout);

        Add(MakeLabel("Welkom bij DeskVeil", 16f, FontStyle.Bold, LightTheme.Text));
        Add(MakeLabel(
            "DeskVeil draait nu in het systeemvak, rechtsonder (soms verborgen achter ^). " +
            "Klik met de rechtermuisknop op het slot-icoon en klik op een bureaublad om het te vergrendelen. " +
            "Kom je daarna op dat bureaublad, dan verschijnt een donker gordijn en vraagt Windows Hello wie je bent.",
            10f, FontStyle.Regular, LightTheme.Text));
        Add(MakeLabel(
            "DeskVeil is een afschrikmiddel tegen meekijkers, geen beveiligingsgrens.",
            10f, FontStyle.Bold, LightTheme.Text));

        var auditLabel = MakeLabel(audit.Describe(), 10f, FontStyle.Regular, audit.Leaks ? LightTheme.WarningText : LightTheme.OkText);
        auditLabel.BackColor = audit.Leaks ? LightTheme.WarningBackground : LightTheme.OkBackground;
        auditLabel.Padding = new Padding(10);
        Add(auditLabel);

        _helloStatus = MakeLabel("Windows Hello controleren…", 10f, FontStyle.Regular, LightTheme.MutedText);
        Add(_helloStatus);

        _openSignInOptions = new Button { Text = "Aanmeldingsopties openen", AutoSize = true, Visible = false, Margin = new Padding(0, 0, 0, 12) };
        _openSignInOptions.Click += (_, _) => Dialogs.Open(Dialogs.SignInOptionsUri);
        Add(_openSignInOptions);

        var ok = new Button
        {
            Text = "Begrepen",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            BackColor = LightTheme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 4, 12, 4),
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => Close();
        Add(ok);
        AcceptButton = ok;

        _ = ShowHelloStatusAsync(helloStatus);
    }

    private async Task ShowHelloStatusAsync(Task<VerifierStatus> helloStatus)
    {
        VerifierStatus status;
        try
        {
            status = await helloStatus;
        }
        catch (Exception ex)
        {
            Log.Error("Checking Windows Hello for the welcome window failed", ex);
            status = new VerifierStatus(false, false, ex.Message);
        }

        if (IsDisposed)
        {
            return;
        }
        _helloStatus.Text = status.CanVerify
            ? "Windows Hello is ingesteld."
            : status.Detail ?? "Windows Hello is niet beschikbaar.";
        _helloStatus.ForeColor = status.CanVerify ? LightTheme.OkText : LightTheme.WarningText;
        _openSignInOptions.Visible = !status.CanVerify && status.UserCanSetUp;
    }

    private void Add(Control control) => _layout.Controls.Add(control);

    private Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(LogicalToDeviceUnits(480), 0),
        Font = new Font("Segoe UI", size, style),
        ForeColor = color,
        BackColor = LightTheme.Background,
        Margin = new Padding(0, 0, 0, 12),
    };
}
