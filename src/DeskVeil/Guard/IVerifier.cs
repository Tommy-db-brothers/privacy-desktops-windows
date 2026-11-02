namespace DeskVeil.Guard;

public enum VerificationOutcome
{
    Verified,
    Canceled,
    Failed,

    /// <summary>No usable verification method (Hello not set up, no device, disabled by policy).</summary>
    Unavailable,
}

/// <param name="Detail">A user-facing explanation when the outcome is not <see cref="VerificationOutcome.Verified"/>.</param>
public sealed record VerificationResult(VerificationOutcome Outcome, string? Detail = null)
{
    public static VerificationResult Verified { get; } = new(VerificationOutcome.Verified);
}

/// <param name="CanVerify">True when a prompt can be shown (possibly after a short wait, e.g. a busy device).</param>
/// <param name="UserCanSetUp">True when the user can fix it in Settings > Accounts > Sign-in options.</param>
/// <param name="Detail">A user-facing explanation when <paramref name="CanVerify"/> is false.</param>
public sealed record VerifierStatus(bool CanVerify, bool UserCanSetUp, string? Detail)
{
    public static VerifierStatus Ready { get; } = new(true, false, null);
}

public interface IVerifier
{
    Task<VerifierStatus> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <param name="ownerWindow">Window that owns the prompt, or 0 to let the verifier provide one.</param>
    /// <param name="message">Shown in the prompt.</param>
    /// <param name="cancellationToken">Cancels the prompt; the result is then <see cref="VerificationOutcome.Canceled"/>.</param>
    Task<VerificationResult> VerifyAsync(nint ownerWindow, string message, CancellationToken cancellationToken = default);
}
