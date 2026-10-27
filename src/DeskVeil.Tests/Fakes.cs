using DeskVeil.Config;
using DeskVeil.Desktops;
using DeskVeil.Guard;

namespace DeskVeil.Tests;

internal sealed class FakeDesktopService : IDesktopService
{
    public FakeDesktopService(params string[] names)
    {
        Desktops = names.Select((name, index) => new DesktopInfo(Guid.NewGuid(), index, name)).ToList();
        Current = Desktops.Count > 0 ? Desktops[0].Id : null;
    }

    public event EventHandler<DesktopChangedEventArgs>? CurrentDesktopChanged;

    public List<DesktopInfo> Desktops { get; }

    public Guid? Current { get; set; }

    public bool Started { get; private set; }

    public List<Guid> SwitchRequests { get; } = [];

    public Guid this[string name] => Desktops.Single(d => d.Name == name).Id;

    public int ReconnectCount { get; private set; }

    public bool FailReconnect { get; set; }

    public void Start() => Started = true;

    public void Reconnect()
    {
        ReconnectCount++;
        if (FailReconnect)
        {
            throw new InvalidOperationException("RegisterPostMessageHook failed.");
        }
    }

    public IReadOnlyList<DesktopInfo> GetDesktops() => Desktops.ToList();

    public Guid? GetCurrentDesktopId() => Current;

    public bool SwitchTo(Guid desktopId)
    {
        SwitchRequests.Add(desktopId);
        if (Desktops.All(d => d.Id != desktopId))
        {
            return false;
        }
        Arrive(desktopId);
        return true;
    }

    /// <summary>Simulates a switch by any means (keyboard, Task View, taskbar).</summary>
    public void Arrive(Guid? desktopId)
    {
        var previous = Current;
        Current = desktopId;
        CurrentDesktopChanged?.Invoke(this, new DesktopChangedEventArgs(previous, desktopId));
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeCurtain : ICurtain
{
    public const nint Handle = 0x1234;

    public event EventHandler? UnlockRequested;

    public event EventHandler? BackRequested;

    public CurtainContent? Content { get; private set; }

    public bool IsShown => Content is not null;

    public nint OwnerHandle => IsShown ? Handle : 0;

    public void Show(CurtainContent content) => Content = content;

    public void Hide() => Content = null;

    public void ClickUnlock() => UnlockRequested?.Invoke(this, EventArgs.Empty);

    public void ClickBack() => BackRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Each call gets a pending prompt that the test completes explicitly.</summary>
internal sealed class FakeVerifier : IVerifier
{
    public List<Prompt> Prompts { get; } = [];

    public Prompt Last => Prompts[^1];

    public VerifierStatus Status { get; set; } = VerifierStatus.Ready;

    public Task<VerifierStatus> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);

    public Task<VerificationResult> VerifyAsync(nint ownerWindow, string message, CancellationToken cancellationToken = default)
    {
        var prompt = new Prompt(ownerWindow, message, cancellationToken);
        Prompts.Add(prompt);
        return prompt.Task;
    }

    internal sealed class Prompt
    {
        private readonly TaskCompletionSource<VerificationResult> _completion = new();

        public Prompt(nint owner, string message, CancellationToken token)
        {
            Owner = owner;
            Message = message;
            Token = token;
            token.Register(() => _completion.TrySetResult(new VerificationResult(VerificationOutcome.Canceled)));
        }

        public nint Owner { get; }

        public string Message { get; }

        public CancellationToken Token { get; }

        public Task<VerificationResult> Task => _completion.Task;

        public void Complete(VerificationOutcome outcome, string? detail = null) =>
            _completion.TrySetResult(new VerificationResult(outcome, detail));
    }
}

internal sealed class InMemoryConfigStore : IConfigStore
{
    public DeskVeilConfig Config { get; } = new();

    public int SaveCount { get; private set; }

    public void Save() => SaveCount++;
}
