using DeskVeil.Desktops;
using DeskVeil.Diagnostics;

namespace DeskVeil.Guard;

/// <summary>
/// <see cref="ICurtain"/> with one <see cref="CurtainWindow"/> per monitor. The windows are created
/// up front and only shown on demand, so the curtain appears without construction cost.
/// Must be used on the UI thread.
/// </summary>
internal sealed class CurtainManager : ICurtain, IDisposable
{
    private readonly List<CurtainWindow> _windows = [];
    private SynchronizationContext? _uiContext;
    private CurtainContent? _content;
    private string _screenSignature = string.Empty;

    public CurtainManager() => Rebuild();

    public event EventHandler? UnlockRequested;

    public event EventHandler? BackRequested;

    public bool IsShown => _content is not null;

    public nint OwnerHandle => IsShown && Primary is { } primary ? primary.Handle : 0;

    private CurtainWindow? Primary => _windows.FirstOrDefault(w => w.Screen.Primary) ?? _windows.FirstOrDefault();

    public void Show(CurtainContent content)
    {
        var appearing = _content is null;
        _content = content;
        if (CurrentScreenSignature() != _screenSignature
            || _windows.Any(w => w.IsDisposed)
            || (appearing && AnyWindowOffCurrentDesktop()))
        {
            Rebuild();
        }

        foreach (var window in _windows)
        {
            window.Apply(content);
            if (!window.Visible)
            {
                window.Show();
            }
        }

        if (appearing)
        {
            foreach (var window in _windows)
            {
                ForegroundHelper.RaiseTopmost(window.Handle);
            }
            if (Primary is { } primary)
            {
                ForegroundHelper.BringToForeground(primary.Handle);
            }
        }
    }

    public void Hide()
    {
        _content = null;
        foreach (var window in _windows)
        {
            window.Hide();
        }
    }

    public void Dispose()
    {
        _content = null;
        DestroyWindows();
    }

    private void Rebuild()
    {
        DestroyWindows();
        foreach (var screen in Screen.AllScreens.OrderByDescending(s => s.Primary))
        {
            var window = new CurtainWindow(screen);
            window.UnlockClicked += OnUnlockClicked;
            window.BackClicked += OnBackClicked;
            window.DisplayChanged += OnDisplayChanged;
            _ = window.Handle;
            _windows.Add(window);
        }
        _uiContext ??= SynchronizationContext.Current;
        _screenSignature = CurrentScreenSignature();
        Log.Info($"Curtain prepared for {_windows.Count} monitor(s).");
    }

    private void DestroyWindows()
    {
        foreach (var window in _windows)
        {
            window.UnlockClicked -= OnUnlockClicked;
            window.BackClicked -= OnBackClicked;
            window.DisplayChanged -= OnDisplayChanged;
            if (!window.IsDisposed)
            {
                // Windows may already have closed it during shutdown.
                window.CloseForReal();
                window.Dispose();
            }
        }
        _windows.Clear();
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        // Every curtain window receives the broadcast and it arrives inside a window procedure,
        // so rebuild once, later, outside of it.
        if (_uiContext is null || CurrentScreenSignature() == _screenSignature)
        {
            return;
        }
        _uiContext.Post(_ => ApplyDisplayChange(), null);
    }

    private void ApplyDisplayChange()
    {
        if (CurrentScreenSignature() == _screenSignature)
        {
            return;
        }
        Log.Info("Display configuration changed; rebuilding the curtain.");
        var content = _content;
        Rebuild();
        if (content is not null)
        {
            _content = null;
            Show(content);
        }
    }

    private void OnUnlockClicked(object? sender, EventArgs e) => UnlockRequested?.Invoke(this, EventArgs.Empty);

    private void OnBackClicked(object? sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Tool windows are not bound to a virtual desktop, but if Windows ever reports one of
    /// them as living on another desktop it would stay invisible here, so it is recreated.</summary>
    private bool AnyWindowOffCurrentDesktop() =>
        _windows.Any(w => VirtualDesktopAccessor.IsWindowOnCurrentVirtualDesktop(w.Handle) == 0);

    private static string CurrentScreenSignature() =>
        string.Join('|', Screen.AllScreens.Select(s => $"{s.DeviceName}:{s.Bounds}:{s.Primary}"));
}
