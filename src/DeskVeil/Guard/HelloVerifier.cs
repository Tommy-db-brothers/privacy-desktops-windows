using DeskVeil.Diagnostics;
using Windows.Security.Credentials.UI;

namespace DeskVeil.Guard;

/// <summary>
/// Windows Hello through <see cref="UserConsentVerifier"/>. The prompt is requested with
/// IUserConsentVerifierInterop::RequestVerificationForWindowAsync so it is owned by, and shown in
/// front of, the given window (the curtain). Availability is checked first; when Hello cannot be
/// used the result is <see cref="VerificationOutcome.Unavailable"/> and nothing is unlocked.
/// </summary>
internal sealed class HelloVerifier : IVerifier
{
    public async Task<VerificationResult> VerifyAsync(nint ownerWindow, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync().AsTask(cancellationToken).ConfigureAwait(true);
            if (availability != UserConsentVerifierAvailability.Available)
            {
                Log.Warn($"Windows Hello not available: {availability}.");
                return new VerificationResult(VerificationOutcome.Unavailable, DescribeAvailability(availability));
            }

            using var temporaryOwner = ownerWindow == 0 ? new PromptOwnerWindow() : null;
            var owner = temporaryOwner?.Handle ?? ownerWindow;

            // The prompt is hosted by another process; let it take the foreground from us.
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            var result = await UserConsentVerifierInterop
                .RequestVerificationForWindowAsync(owner, message)
                .AsTask(cancellationToken)
                .ConfigureAwait(true);
            Log.Info($"Windows Hello result: {result}.");
            return Map(result);
        }
        catch (OperationCanceledException)
        {
            return new VerificationResult(VerificationOutcome.Canceled, "Verificatie afgebroken.");
        }
        catch (Exception ex)
        {
            Log.Error("Windows Hello verification failed", ex);
            return new VerificationResult(VerificationOutcome.Failed, $"Windows Hello gaf een fout: {ex.Message}");
        }
    }

    private static VerificationResult Map(UserConsentVerificationResult result) => result switch
    {
        UserConsentVerificationResult.Verified => VerificationResult.Verified,
        UserConsentVerificationResult.Canceled => new(VerificationOutcome.Canceled, "Verificatie geannuleerd."),
        UserConsentVerificationResult.RetriesExhausted => new(VerificationOutcome.Failed, "Te veel mislukte pogingen. Probeer het later opnieuw."),
        UserConsentVerificationResult.DeviceBusy => new(VerificationOutcome.Failed, "Het Windows Hello-apparaat is bezet. Probeer het opnieuw."),
        UserConsentVerificationResult.DeviceNotPresent => new(VerificationOutcome.Unavailable, "Er is geen Windows Hello-apparaat gevonden."),
        UserConsentVerificationResult.NotConfiguredForUser => new(VerificationOutcome.Unavailable, "Windows Hello is niet ingesteld voor deze gebruiker."),
        UserConsentVerificationResult.DisabledByPolicy => new(VerificationOutcome.Unavailable, "Windows Hello is uitgeschakeld door beleid."),
        _ => new(VerificationOutcome.Failed, $"Onverwacht resultaat van Windows Hello: {result}."),
    };

    private static string DescribeAvailability(UserConsentVerifierAvailability availability) => availability switch
    {
        UserConsentVerifierAvailability.DeviceNotPresent => "Er is geen Windows Hello-apparaat gevonden. Dit bureaublad blijft vergrendeld.",
        UserConsentVerifierAvailability.NotConfiguredForUser => "Windows Hello is niet ingesteld (Instellingen > Accounts > Aanmeldingsopties). Dit bureaublad blijft vergrendeld.",
        UserConsentVerifierAvailability.DisabledByPolicy => "Windows Hello is uitgeschakeld door beleid. Dit bureaublad blijft vergrendeld.",
        UserConsentVerifierAvailability.DeviceBusy => "Het Windows Hello-apparaat is bezet. Probeer het zo opnieuw.",
        _ => $"Windows Hello is niet beschikbaar ({availability}). Dit bureaublad blijft vergrendeld.",
    };

    /// <summary>Small, borderless tool window that owns the prompt when no curtain is shown
    /// (for example when removing a lock from the tray menu).</summary>
    private sealed class PromptOwnerWindow : Form
    {
        public PromptOwnerWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
            Bounds = new Rectangle(area.X + area.Width / 2, area.Y + area.Height / 2, 1, 1);
            TopMost = true;
            Show();
            ForegroundHelper.BringToForeground(Handle);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }
}
