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
    private static readonly ConditionalWeakTable<CoreWebView2, NavigationHookState> HookStates = new();

    internal static void Attach(
        CoreWebView2 coreWebView,
        MessengerInstance instance,
        Func<Task> onNavigationCompletedAsync)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(onNavigationCompletedAsync);

        if (HookStates.TryGetValue(coreWebView, out var existing) && existing.Handler is not null)
        {
            coreWebView.NavigationCompleted -= existing.Handler;
        }

        TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs> handler = (sender, args) =>
        {
            if (!args.IsSuccess)
            {
                InstanceConnectionStatusService.Instance.SetError(
                    instance.Id,
                    args.WebErrorStatus.ToString());
                return;
            }

            _ = UiThreadRunner.RunAsync(onNavigationCompletedAsync);
        };

        var state = new NavigationHookState
        {
            Handler = handler,
            InstanceId = instance.Id
        };

        HookStates.Remove(coreWebView);
        HookStates.Add(coreWebView, state);
        coreWebView.NavigationCompleted += handler;
    }

    internal static void Detach(CoreWebView2 coreWebView)
    {
        if (!HookStates.TryGetValue(coreWebView, out var state) || state.Handler is null)
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
