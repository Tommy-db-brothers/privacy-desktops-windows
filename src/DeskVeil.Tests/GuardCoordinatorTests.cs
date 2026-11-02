using DeskVeil.Guard;

namespace DeskVeil.Tests;

public sealed class GuardCoordinatorTests : IDisposable
{
    private readonly FakeDesktopService _desktops = new("Werk", "Privé", "Muziek");
    private readonly InMemoryConfigStore _store = new();
    private readonly FakeCurtain _curtain = new();
    private readonly FakeVerifier _verifier = new();
    private readonly List<string> _notifications = [];
    private readonly GuardCoordinator _guard;

    public GuardCoordinatorTests()
    {
        Werk = _desktops["Werk"];
        Prive = _desktops["Privé"];
        Muziek = _desktops["Muziek"];
        _guard = new GuardCoordinator(_desktops, _store, _curtain, _verifier);
        _guard.Notification += (_, message) => _notifications.Add(message);
    }

    private Guid Werk { get; }

    private Guid Prive { get; }

    private Guid Muziek { get; }

    public void Dispose() => _guard.Dispose();

    private void StartWithLocked(params Guid[] locked)
    {
        foreach (var id in locked)
        {
            _store.Config.SetLocked(id, true);
        }
        _guard.Start();
    }

    [Fact]
    public void Start_starts_the_desktop_service_and_adds_all_desktops_to_the_config()
    {
        _guard.Start();

        Assert.True(_desktops.Started);
        Assert.Equal(3, _store.Config.Desktops.Count);
        Assert.False(_curtain.IsShown);
        Assert.Empty(_verifier.Prompts);
    }

    [Fact]
    public void Arriving_on_an_unlocked_desktop_shows_nothing()
    {
        StartWithLocked(Prive);

        _desktops.Arrive(Muziek);

        Assert.False(_curtain.IsShown);
        Assert.Empty(_verifier.Prompts);
    }

    [Fact]
    public void Arriving_on_a_locked_desktop_raises_the_curtain_and_prompts_with_the_curtain_as_owner()
    {
        StartWithLocked(Prive);

        _desktops.Arrive(Prive);

        Assert.True(_curtain.IsShown);
        Assert.Equal("Privé", _curtain.Content!.DesktopName);
        Assert.Equal(CurtainState.Verifying, _curtain.Content.State);
        Assert.True(_curtain.Content.CanGoBack);
        var prompt = Assert.Single(_verifier.Prompts);
        Assert.Equal(FakeCurtain.Handle, prompt.Owner);
        Assert.Contains("Privé", prompt.Message);
    }

    [Fact]
    public void Starting_on_a_locked_desktop_enforces_immediately()
    {
        _desktops.Current = _desktops["Privé"];
        StartWithLocked(Prive);

        Assert.True(_curtain.IsShown);
        Assert.Single(_verifier.Prompts);
        Assert.False(_curtain.Content!.CanGoBack);
    }

    [Fact]
    public void Successful_verification_lowers_the_curtain_until_the_desktop_is_left()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);

        _verifier.Last.Complete(VerificationOutcome.Verified);

        Assert.False(_curtain.IsShown);
        Assert.True(_guard.IsUnlockedForSession(Prive));

        _desktops.Arrive(Werk);
        Assert.False(_guard.IsUnlockedForSession(Prive));

        _desktops.Arrive(Prive);
        Assert.True(_curtain.IsShown);
        Assert.Equal(2, _verifier.Prompts.Count);
    }

    [Fact]
    public void With_relock_on_leave_disabled_the_session_unlock_survives_leaving()
    {
        _store.Config.RelockOnLeave = false;
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Verified);

        _desktops.Arrive(Werk);
        _desktops.Arrive(Prive);

        Assert.False(_curtain.IsShown);
        Assert.Single(_verifier.Prompts);
    }

    [Theory]
    [InlineData(VerificationOutcome.Canceled)]
    [InlineData(VerificationOutcome.Failed)]
    public void Cancelled_or_failed_verification_keeps_the_curtain_and_allows_a_retry(VerificationOutcome outcome)
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);

        _verifier.Last.Complete(outcome, "Niet gelukt");

        Assert.True(_curtain.IsShown);
        Assert.Equal(CurtainState.AwaitingUser, _curtain.Content!.State);
        Assert.Equal("Niet gelukt", _curtain.Content.Message);
        Assert.False(_guard.IsUnlockedForSession(Prive));

        _curtain.ClickUnlock();
        Assert.Equal(2, _verifier.Prompts.Count);
        _verifier.Last.Complete(VerificationOutcome.Verified);
        Assert.False(_curtain.IsShown);
    }

    [Fact]
    public void Unavailable_hello_keeps_the_desktop_locked_and_notifies()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);

        _verifier.Last.Complete(VerificationOutcome.Unavailable, "Windows Hello is niet ingesteld.");

        Assert.True(_curtain.IsShown);
        Assert.Equal(CurtainState.Unavailable, _curtain.Content!.State);
        Assert.False(_guard.IsUnlockedForSession(Prive));
        Assert.Contains("Windows Hello is niet ingesteld.", _notifications);
    }

    [Fact]
    public void Back_switches_to_the_previous_desktop_and_lowers_the_curtain()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Muziek);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Canceled);

        _curtain.ClickBack();

        Assert.Equal([Muziek], _desktops.SwitchRequests);
        Assert.Equal(Muziek, _guard.CurrentDesktopId);
        Assert.False(_curtain.IsShown);
    }

    [Fact]
    public void Back_is_ignored_while_a_prompt_is_open()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);

        _curtain.ClickBack();

        Assert.Empty(_desktops.SwitchRequests);
    }

    [Fact]
    public void Leaving_during_a_prompt_cancels_it_and_ignores_its_late_result()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);
        var prompt = _verifier.Last;

        _desktops.Arrive(Werk);

        Assert.True(prompt.Token.IsCancellationRequested);
        Assert.False(_curtain.IsShown);
        prompt.Complete(VerificationOutcome.Verified);
        Assert.False(_guard.IsUnlockedForSession(Prive));
    }

    [Fact]
    public void Switching_from_one_locked_desktop_to_another_during_a_prompt_prompts_for_the_new_one()
    {
        StartWithLocked(Prive, Muziek);
        _desktops.Arrive(Prive);

        _desktops.Arrive(Muziek);

        Assert.Equal(2, _verifier.Prompts.Count);
        Assert.Contains("Muziek", _verifier.Last.Message);
        Assert.Equal("Muziek", _curtain.Content!.DesktopName);
        _verifier.Last.Complete(VerificationOutcome.Verified);
        Assert.True(_guard.IsUnlockedForSession(Muziek));
        Assert.False(_guard.IsUnlockedForSession(Prive));
    }

    [Fact]
    public void Repeated_events_for_the_same_desktop_do_not_open_a_second_prompt()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);

        _desktops.Arrive(Prive);
        _curtain.ClickUnlock();

        Assert.Single(_verifier.Prompts);
    }

    [Fact]
    public void An_event_without_a_current_desktop_is_ignored()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Canceled);

        _desktops.Arrive(null);

        Assert.True(_curtain.IsShown);
        Assert.Equal(Prive, _guard.CurrentDesktopId);
    }

    [Fact]
    public void Removing_the_focused_desktop_cleans_the_config_on_the_next_event()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Verified);

        // Windows removes "Privé" while it is active and moves to the neighbouring desktop.
        _desktops.Desktops.RemoveAll(d => d.Id == Prive);
        var added = new DeskVeil.Desktops.DesktopInfo(Guid.NewGuid(), 2, "Nieuw");
        _desktops.Desktops.Add(added);
        _desktops.Arrive(_desktops.Desktops[0].Id);

        Assert.False(_store.Config.Desktops.ContainsKey(Prive));
        Assert.True(_store.Config.Desktops.ContainsKey(added.Id));
        Assert.False(_guard.IsUnlockedForSession(Prive));
        Assert.False(_curtain.IsShown);
    }

    [Fact]
    public void Back_to_a_removed_desktop_is_refused_with_a_message()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Muziek);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Canceled);
        _desktops.Desktops.RemoveAll(d => d.Id == Muziek);
        _guard.RefreshDesktops();

        _curtain.ClickBack();

        Assert.Empty(_desktops.SwitchRequests);
        Assert.Single(_notifications);
    }

    [Fact]
    public async Task Locking_the_current_desktop_applies_from_the_next_arrival()
    {
        _guard.Start();

        await _guard.LockCurrentDesktopAsync();

        Assert.True(_guard.IsLocked(Werk));
        Assert.False(_curtain.IsShown);
        Assert.Empty(_verifier.Prompts);

        _desktops.Arrive(Prive);
        _desktops.Arrive(Werk);
        Assert.True(_curtain.IsShown);
        Assert.Single(_verifier.Prompts);
    }

    [Fact]
    public async Task Locking_saves_the_config()
    {
        _guard.Start();
        var saves = _store.SaveCount;

        await _guard.ToggleLockAsync(Muziek);

        Assert.True(_store.Config.IsLocked(Muziek));
        Assert.Equal(saves + 1, _store.SaveCount);
    }

    [Fact]
    public async Task Removing_a_lock_requires_verification()
    {
        StartWithLocked(Muziek);

        var refused = _guard.ToggleLockAsync(Muziek);
        Assert.Equal(0, _verifier.Last.Owner);
        _verifier.Last.Complete(VerificationOutcome.Failed, "Nee");
        await refused;
        Assert.True(_guard.IsLocked(Muziek));
        Assert.Contains("Nee", _notifications);

        var accepted = _guard.ToggleLockAsync(Muziek);
        _verifier.Last.Complete(VerificationOutcome.Verified);
        await accepted;
        Assert.False(_guard.IsLocked(Muziek));
    }

    [Fact]
    public async Task Locking_without_a_verification_method_asks_to_set_up_hello_and_does_not_lock()
    {
        _guard.Start();
        VerifierStatus? raised = null;
        _guard.VerifierSetupRequired += (_, status) => raised = status;
        _verifier.Status = new VerifierStatus(false, true, "Windows Hello is niet ingesteld.");

        await _guard.ToggleLockAsync(Prive);

        Assert.False(_guard.IsLocked(Prive));
        Assert.NotNull(raised);
        Assert.True(raised!.UserCanSetUp);
        Assert.Equal("Windows Hello is niet ingesteld.", raised.Detail);
    }

    [Fact]
    public void Shell_restart_reconnects_and_rereads_the_desktops()
    {
        StartWithLocked(Muziek);
        _desktops.Desktops.RemoveAll(d => d.Id == Muziek);

        _guard.HandleShellRestarted();

        Assert.Equal(1, _desktops.ReconnectCount);
        Assert.False(_store.Config.Desktops.ContainsKey(Muziek));
        Assert.False(_curtain.IsShown);
    }

    [Fact]
    public void Shell_restart_enforces_at_once_when_the_current_desktop_is_locked()
    {
        StartWithLocked(Prive);

        // The switch happened while the shell was down, so no event arrived for it.
        _desktops.Current = Prive;
        _guard.HandleShellRestarted();

        Assert.Equal(Prive, _guard.CurrentDesktopId);
        Assert.True(_curtain.IsShown);
        Assert.Equal("Privé", _curtain.Content!.DesktopName);
        Assert.Single(_verifier.Prompts);
    }

    [Fact]
    public void Shell_restart_keeps_a_desktop_that_was_unlocked_for_this_session_open()
    {
        StartWithLocked(Prive);
        _desktops.Arrive(Prive);
        _verifier.Last.Complete(VerificationOutcome.Verified);

        _guard.HandleShellRestarted();

        Assert.False(_curtain.IsShown);
        Assert.Single(_verifier.Prompts);
    }

    [Fact]
    public void Shell_restart_with_a_failed_reconnect_notifies_and_still_enforces()
    {
        StartWithLocked(Prive);
        _desktops.FailReconnect = true;
        _desktops.Current = Prive;

        _guard.HandleShellRestarted();

        Assert.Single(_notifications);
        Assert.True(_curtain.IsShown);
    }
}
