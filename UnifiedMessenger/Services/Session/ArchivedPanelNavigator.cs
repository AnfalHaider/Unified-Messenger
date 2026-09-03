using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Opens and closes WhatsApp's archived conversations panel — the second named navigation, and the one
/// built to the pattern rather than retrofitted onto it.
/// </summary>
/// <remarks>
/// <para>Its readback was measured live rather than guessed, which changed the answer. The obvious signals
/// are both wrong: the row count under <c>#pane-side</c> does <b>not</b> change when the panel opens (the
/// main list stays underneath it), and the Back button that appears is generic chrome that other views
/// show too. The panel has its own container, <c>[data-testid="archived-chatlist"]</c>, and that is the
/// only thing whose presence means this view specifically is on screen.</para>
/// <para>Read-only: it opens a list, not a conversation, so nothing is marked read and no receipt fires.
/// That is why it is the one operation here that does not require user intent.</para>
/// </remarks>
public static class ArchivedPanelNavigator
{
    private const string OpenScript =
        "(function(){try{" +
        "var b=window.__umPick?window.__umPick('archivedButton','[data-testid=\"chatlist-panel-archived-button\"]')[0]" +
        ":document.querySelector('[data-testid=\"chatlist-panel-archived-button\"]');" +
        "if(!b)return false;b.click();return true;}catch(e){return false;}})()";

    private const string CloseScript =
        "(function(){try{" +
        "var b=window.__umPick?window.__umPick('backButton','[aria-label=\"Back\"]')[0]" +
        ":document.querySelector('[aria-label=\"Back\"]');" +
        "if(!b)return false;b.click();return true;}catch(e){return false;}})()";

    /// <summary>Opens the archived panel and returns only once an independent readback confirms it.</summary>
    public static Task<NavigationOutcome> OpenAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        bool userInitiated = true,
        CancellationToken cancellationToken = default) =>
        RunAsync(sessionManager, instance, OpenScript, expectPanel: true, userInitiated, cancellationToken);

    /// <summary>Closes it again, verified the same way — arrival back at the main list is the panel's absence.</summary>
    public static Task<NavigationOutcome> CloseAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        CancellationToken cancellationToken = default) =>
        RunAsync(sessionManager, instance, CloseScript, expectPanel: false, userInitiated: true, cancellationToken);

    private static async Task<NavigationOutcome> RunAsync(
        IInstanceSessionManager sessionManager,
        MessengerInstance instance,
        string clickScript,
        bool expectPanel,
        bool userInitiated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(instance);

        var operation = NavigationOperations.Require(NavigationOperations.ShowArchived);
        if (!NavigationOperations.MayRun(operation, userInitiated))
        {
            return NavigationOutcome.Refused(operation.Id);
        }

        var readback = NavigationOperations.BuildReadbackScript(operation);

        for (var attempt = 1; attempt <= operation.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await sessionManager.TryExecuteScriptOnInstanceAsync(instance.Id, clickScript).ConfigureAwait(false);
            await Task.Delay(operation.RetryDelayMs, cancellationToken).ConfigureAwait(false);

            var raw = await sessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, readback)
                .ConfigureAwait(false);

            var panelPresent = string.Equals(raw?.Trim().Trim('"'), "true", StringComparison.Ordinal);
            if (panelPresent == expectPanel)
            {
                AppLogger.LogInfo(
                    "Navigate",
                    $"{instance.DisplayName}: {operation.Id} want={(expectPanel ? "open" : "closed")} "
                    + $"reached={(panelPresent ? "open" : "closed")} attempts={attempt}/{operation.MaxAttempts} "
                    + $"budget={operation.Budget.TotalSeconds:0.#}s");

                return new NavigationOutcome(true, true, expectPanel ? "archived" : "chat-list", attempt, operation.Id);
            }
        }

        AppLogger.LogWarning(
            "Navigate",
            $"{instance.DisplayName}: {operation.Id} did not reach {(expectPanel ? "the archived panel" : "the chat list")} "
            + $"within {operation.Budget.TotalSeconds:0.#}s.");

        return new NavigationOutcome(false, false, "not-reached", operation.MaxAttempts, operation.Id);
    }
}
