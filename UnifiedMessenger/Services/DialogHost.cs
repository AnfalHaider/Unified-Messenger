using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Serialises <see cref="ContentDialog"/> display so two dialogs can never be open at once.
///
/// <para>
/// <b>The crash this closes.</b> WinUI permits exactly one <c>ContentDialog</c> at a time. Calling
/// <c>ShowAsync</c> while another is open throws <c>COMException (0x80000019)</c>, and the app had
/// <b>31 unguarded call sites</b> with no coordination between them. One instance reached the global
/// unhandled-exception handler on the owner's machine:
/// </para>
/// <code>
/// [ERR] [App.UnhandledException] System.Runtime.InteropServices.COMException (0x80000019):
///       An async operation was not properly started.
/// </code>
/// <para>
/// It is a race, so it fires only when two prompts coincide — which is exactly what the startup path does
/// (settings-recovery notice, onboarding, pin prompt), and what any background trigger does when it lands
/// while the owner already has a dialog open. <c>ShellController.RunStartupPromptsAsync</c> sequenced the
/// three startup prompts by hand; that helped those three and did nothing for the other twenty-eight.
/// </para>
/// <para>
/// <b>Queue rather than drop.</b> An earlier finding (F-DURA-03) was that racing prompts silently swallowed
/// one of them. Serialising by waiting preserves both: the second dialog opens when the first closes, so a
/// notice the owner needs to see is delayed, never lost.
/// </para>
/// </summary>
public static class DialogHost
{
    // A plain SemaphoreSlim, not a lock: the wait has to be awaitable, and dialogs are shown from async UI
    // code. Never disposed — it lives for the process, like the window it guards.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// How long a queued dialog waits. Long enough that a real dialog the owner is reading is never
    /// abandoned, short enough that a programming mistake is not a frozen window.
    /// </summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromMinutes(2);

    /// <summary>True while a dialog is on screen, for callers that would rather skip than queue.</summary>
    public static bool IsShowing => Gate.CurrentCount == 0;

    /// <summary>
    /// Shows a dialog, waiting for any dialog already on screen to close first.
    /// </summary>
    public static async Task<ContentDialogResult> ShowManagedAsync(this ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        // Bounded, not indefinite. Every call site found is sequential — "await the confirm, then act" —
        // so the gate is always released before the next show. But a NESTED show (opening a dialog from
        // inside an open dialog's button handler) would wait on a gate only that dialog can release, and an
        // unbounded wait would turn that into a permanent hang. WinUI cannot display two dialogs at once
        // anyway, so such a call site would already be broken; this makes it a logged failure with a
        // recognisable message instead of a frozen window.
        if (!await Gate.WaitAsync(GateTimeout).ConfigureAwait(true))
        {
            AppLogger.LogWarning(
                "Dialog",
                "Timed out waiting for another dialog to close. This usually means a dialog was opened from "
                + "inside another dialog's handler, which WinUI does not allow.");
            return ContentDialogResult.None;
        }

        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // The gate makes the documented 0x80000019 impossible, but a dialog can still fail to show for
            // reasons outside our control — a closing window, a torn-down XamlRoot during shutdown. Those
            // must not become an unhandled exception on a background continuation, which is how this
            // surfaced in the first place.
            AppLogger.LogWarning("Dialog", $"Could not show dialog: {ex.GetType().Name}: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Shows a dialog only if nothing else is on screen, otherwise returns immediately without showing it.
    /// </summary>
    /// <remarks>
    /// For interruptions the owner did not ask for — a background alert, a reminder. Queuing those behind a
    /// dialog the owner is actively working in makes the queued one appear unprompted several seconds later,
    /// attached to nothing. Anything the owner explicitly triggered should use
    /// <see cref="ShowManagedAsync"/> and wait its turn.
    /// </remarks>
    public static async Task<ContentDialogResult> ShowIfFreeAsync(this ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (!Gate.Wait(0))
        {
            AppLogger.LogInfo("Dialog", "Skipped a background dialog because another was already open.");
            return ContentDialogResult.None;
        }

        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Dialog", $"Could not show dialog: {ex.GetType().Name}: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            Gate.Release();
        }
    }
}
