using System.Runtime.CompilerServices;
using Windows.Foundation;
using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters;

/// <summary>
/// Tracks <see cref="CoreWebView2.NavigationCompleted"/> handlers so they can be detached on session dispose.
/// </summary>
internal static class PlatformNavigationHooks
{
    /// <summary>
    /// Keyed by instance id, deliberately — NOT by <see cref="CoreWebView2"/>.
    /// </summary>
    /// <remarks>
    /// This was a <c>ConditionalWeakTable&lt;CoreWebView2, …&gt;</c>, which AGENTS.md records as a known
    /// bug class: <c>CoreWebView2</c> is a CsWinRT projection, so the managed wrapper can be collected and
    /// re-created for the same native object, silently dropping the entry. Here that is not merely a leak.
    /// <c>BasePlatformAdapter.RegisterAsync</c> guards re-registration with a table keyed the same way, so
    /// both entries vanish together: the adapter re-registers on a LIVE WebView, <c>Attach</c> fails to
    /// find the previous handler to remove, and the account ends up with two <c>NavigationCompleted</c>
    /// handlers — two <c>NavigationRetryScheduler</c> calls and two status writes per navigation.
    /// <para>
    /// A string key is safe here in a way it would not be for the registration guards. A stale entry in
    /// this table only causes one extra <c>-=</c> of a handler that is already gone, which does nothing.
    /// A stale entry in a registration guard would mean "already registered" for a WebView that is not —
    /// an account that silently never scrapes again. That asymmetry is why this one moved and those did
    /// not; see the note on <c>BasePlatformAdapter.RegisteredHosts</c>.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, NavigationHookState> HookStates =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly object HookLock = new();

    internal static void Attach(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        Func<Task> onNavigationCompletedAsync)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(onNavigationCompletedAsync);

        // Remove whatever this account had before. Looked up by id, so a re-registration on a live WebView
        // cannot end up with two handlers just because a projection wrapper was collected in between.
        NavigationHookState? existing;
        lock (HookLock)
        {
            HookStates.Remove(instance.Id, out existing);
        }

        if (existing?.Handler is not null)
        {
            coreWebView.NavigationCompleted -= existing.Handler;
        }

        TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs> handler = (sender, args) =>
        {
            if (!args.IsSuccess)
            {
                var webErrorStatus = args.WebErrorStatus.ToString();

                // Every failure is logged, not only the retryable ones. The scheduler stays quiet for
                // statuses it will not retry, which meant a reload that cancelled an in-flight navigation
                // recorded a different status and left no trace of having done so — the one thing that
                // made the "Connection error" regression below hard to see.
                AppLogger.LogInfo("WebView.Nav", $"'{instance.Id}' navigation failed ({webErrorStatus}).");

                InstanceConnectionStatusService.Instance.SetError(instance.Id, webErrorStatus);

                // Nothing else retries this. The stale-adapter monitor only watches accounts that reached
                // Ready, and an account whose page never loaded has no adapter to go stale — so without
                // this a transient drop left the account dead until the owner refreshed it by hand.
                NavigationRetryScheduler.Instance.OnNavigationFailed(instance.Id, webErrorStatus);
                return;
            }

            NavigationRetryScheduler.Instance.OnNavigationSucceeded(instance.Id);
            _ = UiThreadRunner.RunAsync(onNavigationCompletedAsync);
        };

        var state = new NavigationHookState
        {
            Handler = handler,
            InstanceId = instance.Id
        };

        lock (HookLock)
        {
            HookStates[instance.Id] = state;
        }

        coreWebView.NavigationCompleted += handler;
    }

    internal static void Detach(CoreWebView2 coreWebView, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        NavigationHookState? state;
        lock (HookLock)
        {
            if (!HookStates.Remove(instanceId ?? string.Empty, out state))
            {
                return;
            }
        }

        if (state.Handler is null)
        {
            return;
        }

        coreWebView.NavigationCompleted -= state.Handler;
        state.Handler = null;
    }

    private sealed class NavigationHookState
    {
        public TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs>? Handler { get; set; }

        public string InstanceId { get; init; } = string.Empty;
    }
}
