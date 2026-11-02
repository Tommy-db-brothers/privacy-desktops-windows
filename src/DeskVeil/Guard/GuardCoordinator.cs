using DeskVeil.Config;
using DeskVeil.Desktops;
using DeskVeil.Diagnostics;

namespace DeskVeil.Guard;

/// <summary>
/// Enforces locks after a switch: whenever the active desktop is locked and not unlocked for this
/// session, the curtain goes up and a verification prompt follows. Enforcing on arrival covers every
/// switch path (Task View, Win+Ctrl+arrow, taskbar, Alt+Tab). All members run on the UI thread.
/// </summary>
public sealed class GuardCoordinator : IDisposable
{
    private readonly IDesktopService _desktops;
    private readonly IConfigStore _store;
    private readonly ICurtain _curtain;
    private readonly IVerifier _verifier;
    private readonly HashSet<Guid> _unlockedForSession = [];
    private IReadOnlyList<DesktopInfo> _knownDesktops = [];
    private CancellationTokenSource? _verification;
    private Guid? _current;
    private Guid? _previous;
    private bool _started;

    public GuardCoordinator(IDesktopService desktops, IConfigStore store, ICurtain curtain, IVerifier verifier)
    {
        _desktops = desktops;
        _store = store;
        _curtain = curtain;
        _verifier = verifier;
    }

    /// <summary>User-facing messages (shown as tray balloons).</summary>
    public event EventHandler<string>? Notification;

    /// <summary>Raised instead of locking when no verification method is available, so the UI can
    /// explain how to set up Windows Hello.</summary>
    public event EventHandler<VerifierStatus>? VerifierSetupRequired;

    public Guid? CurrentDesktopId => _current;

    public bool RelockOnLeave => _store.Config.RelockOnLeave;

    public bool IsVerifying => _verification is not null;

    public bool IsLocked(Guid desktopId) => _store.Config.IsLocked(desktopId);

    public bool IsUnlockedForSession(Guid desktopId) => _unlockedForSession.Contains(desktopId);

    public void Start()
    {
        if (_started)
        {
            return;
        }
        _started = true;

        _desktops.CurrentDesktopChanged += OnCurrentDesktopChanged;
        _curtain.UnlockRequested += OnUnlockRequested;
        _curtain.BackRequested += OnBackRequested;
        _desktops.Start();

        _current = _desktops.GetCurrentDesktopId();
        RefreshDesktops();
        Enforce();
    }

    /// <summary>Re-reads the desktops and brings the configuration in line (removed desktops dropped, new ones added).</summary>
    /// <returns>The current desktops, or the last known list if they could not be read.</returns>
    public IReadOnlyList<DesktopInfo> RefreshDesktops()
    {
        var desktops = _desktops.GetDesktops();
        if (desktops.Count == 0)
        {
            return _knownDesktops;
        }

        _knownDesktops = desktops;
        _unlockedForSession.IntersectWith(desktops.Select(d => d.Id));
        if (_store.Config.Reconcile(desktops))
        {
            Save();
        }
        return desktops;
    }

    /// <summary>
    /// After explorer.exe restarted (TaskbarCreated): registers the switch listener again, re-reads the
    /// desktops and enforces at once, in case the active desktop changed or is locked.
    /// </summary>
    public void HandleShellRestarted()
    {
        Log.Info("The shell restarted; reconnecting to the virtual desktop notifications.");
        try
        {
            _desktops.Reconnect();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error("Reconnecting after a shell restart failed", ex);
            Notify("Na een herstart van Verkenner kan DeskVeil bureaublad-wissels niet meer volgen. Start DeskVeil opnieuw.");
        }

        RefreshDesktops();
        ArriveAt(_desktops.GetCurrentDesktopId());
    }

    /// <summary>
    /// Locks an unlocked desktop, or removes the lock after a successful verification.
    /// Locking the desktop you are on does not raise the curtain; it applies from the next arrival.
    /// A lock is only set when a verification method exists; otherwise <see cref="VerifierSetupRequired"/> is raised.
    /// </summary>
    public async Task ToggleLockAsync(Guid desktopId)
    {
        var name = NameOf(desktopId);
        if (!IsLocked(desktopId))
        {
            var status = await CheckVerifierAsync();
            if (!status.CanVerify)
            {
                Log.Warn($"Not locking desktop {desktopId}: no verification method ({status.Detail}).");
                VerifierSetupRequired?.Invoke(this, status);
                return;
            }

            _store.Config.SetLocked(desktopId, true, name);
            if (desktopId == _current)
            {
                _unlockedForSession.Add(desktopId);
            }
            Save();
            Log.Info($"Desktop {desktopId} '{name}' locked.");
            return;
        }

        if (_verification is not null)
        {
            return;
        }

        var result = await RunVerificationAsync(0, $"Vergrendeling van '{name}' opheffen");
        if (result.Outcome == VerificationOutcome.Verified)
        {
            _store.Config.SetLocked(desktopId, false, name);
            _unlockedForSession.Remove(desktopId);
            Save();
            Log.Info($"Desktop {desktopId} '{name}' unlocked permanently.");
            Enforce();
        }
        else if (result.Outcome != VerificationOutcome.Canceled)
        {
            Notify(result.Detail ?? $"De vergrendeling van '{name}' is niet opgeheven.");
        }
    }

    public Task LockCurrentDesktopAsync() =>
        _current is Guid id && !IsLocked(id) ? ToggleLockAsync(id) : Task.CompletedTask;

    public void SetRelockOnLeave(bool enabled)
    {
        _store.Config.RelockOnLeave = enabled;
        Save();
    }

    public void Dispose()
    {
        _desktops.CurrentDesktopChanged -= OnCurrentDesktopChanged;
        _curtain.UnlockRequested -= OnUnlockRequested;
        _curtain.BackRequested -= OnBackRequested;
        _verification?.Cancel();
    }

    private void OnCurrentDesktopChanged(object? sender, DesktopChangedEventArgs e)
    {
        RefreshDesktops();
        ArriveAt(e.CurrentId);
    }

    private void ArriveAt(Guid? currentId)
    {
        if (currentId is not Guid arrived)
        {
            // The shell is mid-transition (for example the active desktop is being removed);
            // the event that follows the transition settles the state.
            return;
        }
        if (arrived == _current)
        {
            Enforce();
            return;
        }

        var left = _current;
        _current = arrived;
        if (left is Guid leftId)
        {
            _previous = leftId;
            if (_store.Config.RelockOnLeave)
            {
                _unlockedForSession.Remove(leftId);
            }
        }

        // A prompt that is still open belongs to the desktop we just left.
        _verification?.Cancel();
        Enforce();
    }

    private void OnUnlockRequested(object? sender, EventArgs e)
    {
        if (_current is Guid id && NeedsVerification(id))
        {
            _ = VerifyCurrentDesktopAsync();
        }
    }

    private void OnBackRequested(object? sender, EventArgs e)
    {
        if (_verification is not null)
        {
            return;
        }
        if (!CanGoBack || _previous is not Guid previous)
        {
            Notify("De vorige desktop bestaat niet meer.");
            return;
        }
        if (!_desktops.SwitchTo(previous))
        {
            Notify("Terugschakelen naar de vorige desktop is mislukt.");
        }
    }

    private bool NeedsVerification(Guid desktopId) => IsLocked(desktopId) && !_unlockedForSession.Contains(desktopId);

    private bool CanGoBack =>
        _previous is Guid previous && previous != _current && _knownDesktops.Any(d => d.Id == previous);

    private void Enforce()
    {
        if (_current is not Guid id || !NeedsVerification(id))
        {
            _curtain.Hide();
            return;
        }

        ShowCurtain(id, CurtainState.Verifying, null);
        _ = VerifyCurrentDesktopAsync();
    }

    private async Task VerifyCurrentDesktopAsync()
    {
        if (_verification is not null || _current is not Guid id)
        {
            return;
        }

        ShowCurtain(id, CurtainState.Verifying, null);
        var result = await RunVerificationAsync(_curtain.OwnerHandle, $"Ontgrendel bureaublad '{NameOf(id)}'");

        if (_current != id || !NeedsVerification(id))
        {
            // We left the desktop (or its lock was lifted) while the prompt was open: the result is stale.
            Enforce();
            return;
        }

        switch (result.Outcome)
        {
            case VerificationOutcome.Verified:
                _unlockedForSession.Add(id);
                _curtain.Hide();
                Log.Info($"Desktop {id} unlocked for this session.");
                break;
            case VerificationOutcome.Unavailable:
                ShowCurtain(id, CurtainState.Unavailable, result.Detail);
                Notify(result.Detail ?? "Windows Hello is niet beschikbaar; het bureaublad blijft vergrendeld.");
                break;
            default:
                ShowCurtain(id, CurtainState.AwaitingUser, result.Detail);
                break;
        }
    }

    private async Task<VerificationResult> RunVerificationAsync(nint owner, string message)
    {
        using var cancellation = new CancellationTokenSource();
        _verification = cancellation;
        try
        {
            return await _verifier.VerifyAsync(owner, message, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return new VerificationResult(VerificationOutcome.Canceled);
        }
        catch (Exception ex)
        {
            Log.Error("Verification threw", ex);
            return new VerificationResult(VerificationOutcome.Failed, ex.Message);
        }
        finally
        {
            _verification = null;
        }
    }

    private async Task<VerifierStatus> CheckVerifierAsync()
    {
        try
        {
            return await _verifier.CheckAvailabilityAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Checking the verifier failed", ex);
            return new VerifierStatus(false, false, ex.Message);
        }
    }

    private void ShowCurtain(Guid desktopId, CurtainState state, string? message) =>
        _curtain.Show(new CurtainContent(NameOf(desktopId), state, message, CanGoBack));

    private string NameOf(Guid desktopId) =>
        _knownDesktops.FirstOrDefault(d => d.Id == desktopId)?.Name
        ?? _store.Config.Desktops.GetValueOrDefault(desktopId)?.LastKnownName
        ?? "Onbekend bureaublad";

    private void Save()
    {
        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Saving the configuration failed", ex);
            Notify($"De instellingen konden niet worden opgeslagen: {ex.Message}");
        }
    }

    private void Notify(string message) => Notification?.Invoke(this, message);
}
