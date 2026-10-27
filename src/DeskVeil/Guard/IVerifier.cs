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

public interface IVerifier
{
    /// <param name="ownerWindow">Window that owns the prompt, or 0 to let the verifier provide one.</param>
    /// <param name="message">Shown in the prompt.</param>
    /// <param name="cancellationToken">Cancels the prompt; the result is then <see cref="VerificationOutcome.Canceled"/>.</param>
    Task<VerificationResult> VerifyAsync(nint ownerWindow, string message, CancellationToken cancellationToken = default);
}
