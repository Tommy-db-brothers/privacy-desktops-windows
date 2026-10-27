using System.Runtime.InteropServices;

namespace ZBandProbe;

/// <summary>
/// Shows a red 200x200 window in the top-left corner with HWND_TOPMOST and WS_EX_TOPMOST, nothing else.
/// Open Task View (Win+Tab) and check whether the square stays visible. If it disappears, Task View
/// lives in a shell z-band above HWND_TOPMOST and an ordinary app cannot draw over it.
/// </summary>
internal static partial class Program
{
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private static readonly nint HWND_TOPMOST = -1;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var probe = new ProbeWindow();

        Console.WriteLine("ZBandProbe: rood vlak (200x200) linksboven, HWND_TOPMOST + WS_EX_TOPMOST.");
        Console.WriteLine("Open Task View (Win+Tab) en kijk of het rode vlak zichtbaar blijft.");
        Console.WriteLine("  Zichtbaar   -> Task View ligt op deze machine niet boven HWND_TOPMOST.");
        Console.WriteLine("  Verdwenen   -> Task View rendert in een eigen shell-z-band boven HWND_TOPMOST.");
        Console.WriteLine("Druk op Enter om te stoppen.");

        var reader = new Thread(() =>
        {
            Console.ReadLine();
            probe.BeginInvoke(probe.Close);
        })
        { IsBackground = true };
        reader.Start();

        Application.Run(probe);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    private sealed class ProbeWindow : Form
    {
        public ProbeWindow()
        {
            Text = "ZBandProbe";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            var origin = Screen.PrimaryScreen?.Bounds.Location ?? Point.Empty;
            Bounds = new Rectangle(origin, new Size(200, 200));
            BackColor = Color.Red;
            TopMost = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
    }
}
