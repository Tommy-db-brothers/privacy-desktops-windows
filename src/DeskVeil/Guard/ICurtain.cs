namespace DeskVeil.Guard;

public enum CurtainState
{
    /// <summary>A Windows Hello prompt is open.</summary>
    Verifying,

    /// <summary>Verification was cancelled or failed; the user can retry or go back.</summary>
    AwaitingUser,

    /// <summary>Windows Hello cannot be used; the desktop stays locked.</summary>
    Unavailable,
}

public sealed record CurtainContent(string DesktopName, CurtainState State, string? Message, bool CanGoBack);

/// <summary>The privacy curtain that covers every monitor while a locked desktop is active.</summary>
public interface ICurtain
{
    event EventHandler? UnlockRequested;

    event EventHandler? BackRequested;

    bool IsShown { get; }

    /// <summary>Window that owns the verification prompt so it appears in front of the curtain; 0 while hidden.</summary>
    nint OwnerHandle { get; }

    /// <summary>Shows the curtain, or updates it when it is already shown.</summary>
    void Show(CurtainContent content);

    void Hide();
}
