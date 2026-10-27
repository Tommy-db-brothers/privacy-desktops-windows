using System.Diagnostics;
using DeskVeil.Diagnostics;
using DeskVeil.Guard;

namespace DeskVeil.Ui;

internal static class Dialogs
{
    public const string SignInOptionsUri = "ms-settings:signinoptions";

    /// <summary>DeskVeil cannot run here (unsupported build or DLL problem).</summary>
    public static void ShowUnsupported(string message, string supportUrl)
    {
        var openDocs = new TaskDialogButton("Ondersteunde Windows-versies");
        var page = new TaskDialogPage
        {
            Caption = "DeskVeil",
            Heading = "DeskVeil kan niet starten",
            Text = message,
            Icon = TaskDialogIcon.Error,
            Buttons = { openDocs, TaskDialogButton.Close },
            DefaultButton = TaskDialogButton.Close,
        };
        if (TaskDialog.ShowDialog(page) == openDocs)
        {
            Open(supportUrl);
        }
    }

    /// <summary>No verification method: explain and offer to open the sign-in options.</summary>
    public static void ShowHelloSetup(VerifierStatus status)
    {
        var openSettings = new TaskDialogButton("Aanmeldingsopties openen");
        var page = new TaskDialogPage
        {
            Caption = "DeskVeil",
            Heading = "Stel eerst Windows Hello in",
            Text = (status.Detail ?? "Windows Hello is niet beschikbaar.") + "\n\n" +
                   "DeskVeil ontgrendelt een bureaublad alleen met Windows Hello (gezicht, vingerafdruk of pincode). " +
                   "Zonder Hello zou een vergrendeld bureaublad niet meer te openen zijn, dus er is niets vergrendeld.",
            Icon = TaskDialogIcon.Warning,
            DefaultButton = TaskDialogButton.Close,
        };
        if (status.UserCanSetUp)
        {
            page.Buttons.Add(openSettings);
        }
        page.Buttons.Add(TaskDialogButton.Close);

        if (TaskDialog.ShowDialog(page) == openSettings)
        {
            Open(SignInOptionsUri);
        }
    }

    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error($"Opening '{target}' failed", ex);
        }
    }
}
