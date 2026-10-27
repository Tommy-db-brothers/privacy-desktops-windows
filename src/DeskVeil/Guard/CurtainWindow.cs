namespace DeskVeil.Guard;

/// <summary>
/// Borderless, topmost, full-screen window for one monitor. It is a tool window, so it stays out of
/// Alt+Tab and the taskbar and Windows shows it on every virtual desktop. Alt+F4 and the system
/// close command are ignored; only <see cref="CloseForReal"/> or a Windows shutdown closes it.
/// </summary>
internal sealed class CurtainWindow : Form
{
    private static readonly Color s_background = Color.FromArgb(17, 19, 23);
    private static readonly Color s_foreground = Color.FromArgb(236, 238, 241);
    private static readonly Color s_muted = Color.FromArgb(160, 166, 176);
    private static readonly Color s_accent = Color.FromArgb(76, 141, 255);

    private readonly bool _isPrimary;
    private readonly TableLayoutPanel _content;
    private readonly Label _desktopName;
    private readonly Label _status;
    private readonly Button _unlock;
    private readonly Button _back;
    private bool _allowClose;

    public CurtainWindow(Screen screen)
    {
        _isPrimary = screen.Primary;
        Screen = screen;

        Text = "DeskVeil";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        ShowInTaskbar = false;
        TopMost = true;
        ControlBox = false;
        BackColor = s_background;
        ForeColor = s_foreground;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 11f);

        var glyph = new LockGlyphControl { Size = new Size(96, 96), Margin = new Padding(0, 0, 0, 24), Anchor = AnchorStyles.None };
        var title = MakeLabel("Dit bureaublad is vergrendeld", 13f, FontStyle.Regular, s_muted);
        _desktopName = MakeLabel(string.Empty, 28f, FontStyle.Bold, s_foreground);
        _desktopName.Margin = new Padding(0, 4, 0, 16);
        _status = MakeLabel(string.Empty, 12f, FontStyle.Regular, s_muted);
        _status.Margin = new Padding(0, 0, 0, 28);
        _status.MaximumSize = new Size(640, 0);

        _unlock = MakeButton("Ontgrendelen", primary: true);
        _unlock.Click += (_, _) => UnlockClicked?.Invoke(this, EventArgs.Empty);
        _back = MakeButton("Naar vorige desktop", primary: false);
        _back.Click += (_, _) => BackClicked?.Invoke(this, EventArgs.Empty);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            BackColor = s_background,
        };
        buttons.Controls.Add(_unlock);
        buttons.Controls.Add(_back);

        _content = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = s_background,
        };
        foreach (Control control in new Control[] { glyph, title, _desktopName, _status, buttons })
        {
            control.Anchor = AnchorStyles.None;
            _content.Controls.Add(control);
        }
        Controls.Add(_content);
        AcceptButton = _unlock;
    }

    public event EventHandler? UnlockClicked;

    public event EventHandler? BackClicked;

    /// <summary>Raised when the display configuration changes (WM_DISPLAYCHANGE).</summary>
    public event EventHandler? DisplayChanged;

    public Screen Screen { get; }

    protected override bool ShowWithoutActivation => !_isPrimary;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
            cp.ExStyle &= ~NativeMethods.WS_EX_APPWINDOW;
            return cp;
        }
    }

    public void Apply(CurtainContent content)
    {
        _desktopName.Text = content.DesktopName;
        _status.Text = content.Message ?? content.State switch
        {
            CurtainState.Verifying => "Bevestig je identiteit met Windows Hello…",
            CurtainState.AwaitingUser => "Verificatie is niet gelukt. Probeer het opnieuw of ga terug naar de vorige desktop.",
            CurtainState.Unavailable => "Windows Hello is niet beschikbaar. Dit bureaublad blijft vergrendeld.",
            _ => string.Empty,
        };
        _unlock.Enabled = content.State != CurtainState.Verifying;
        _back.Visible = content.CanGoBack;
        _back.Enabled = content.State != CurtainState.Verifying;
        CenterContent();
    }

    /// <summary>Closes the window; the only way besides a Windows shutdown.</summary>
    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        keyData == (Keys.Alt | Keys.F4) || base.ProcessCmdKey(ref msg, keyData);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == NativeMethods.SC_CLOSE)
        {
            return;
        }
        if (m.Msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        CenterContent();
    }

    private void CenterContent() =>
        _content.Location = new Point(
            Math.Max(0, (ClientSize.Width - _content.Width) / 2),
            Math.Max(0, (ClientSize.Height - _content.Height) / 2));

    private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", size, style),
        ForeColor = color,
        TextAlign = ContentAlignment.MiddleCenter,
    };

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18, 8, 18, 8),
            Margin = new Padding(8, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? s_accent : Color.FromArgb(44, 48, 56),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private sealed class LockGlyphControl : Control
    {
        public LockGlyphControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = s_background;
        }

        protected override void OnPaint(PaintEventArgs e) =>
            LockGlyph.Draw(e.Graphics, ClientRectangle, s_foreground, open: false);
    }
}
