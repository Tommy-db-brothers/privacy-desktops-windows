namespace DeskVeil.Ui;

/// <summary>Colours for DeskVeil's own windows (everything except the curtain, which is deliberately dark).</summary>
internal static class LightTheme
{
    public static readonly Color Background = Color.White;
    public static readonly Color Text = Color.FromArgb(28, 30, 33);
    public static readonly Color MutedText = Color.FromArgb(88, 94, 102);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color OkBackground = Color.FromArgb(232, 245, 236);
    public static readonly Color OkText = Color.FromArgb(22, 101, 52);
    public static readonly Color WarningBackground = Color.FromArgb(255, 243, 224);
    public static readonly Color WarningText = Color.FromArgb(146, 64, 14);

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10f);
    }
}
