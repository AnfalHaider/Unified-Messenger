using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ConversationNavigationResult
{
    public bool InstanceOpened { get; init; }

    public bool ConversationFocused { get; init; }

    public string? StatusMessage { get; init; }

    public bool IsFailure => InstanceOpened && !ConversationFocused;
}

/// <summary>
/// Single entry point for KPI, queue, and command-palette thread navigation with key resolution.
/// </summary>
public static class ConversationNavigationCoordinator
{
    private static readonly TimeSpan NavigationLoadingThreshold = TimeSpan.FromMilliseconds(300);

    public static string? ResolveConversationKey(
        IThreadRegistryService threadRegistry,
        string? threadId,
        string? conversationKey,
        string? customerName,
        string instanceId,
        string? platform = null)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
        {
            return conversationKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            var thread = threadRegistry.GetAllThreads()
                .FirstOrDefault(candidate =>
                    candidate.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase));
            if (thread is not null && !string.IsNullOrWhiteSpace(thread.ConversationKey))
            {
                return thread.ConversationKey.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(customerName) && !string.IsNullOrWhiteSpace(instanceId))
        {
            var match = threadRegistry.GetAllThreads()
                .FirstOrDefault(candidate =>
                    candidate.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase) &&
                    candidate.CustomerName.Equals(customerName, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(candidate.ConversationKey));
            if (match is not null)
            {
                return match.ConversationKey.Trim();
            }
        }

        return null;
    }

    public static async Task<ConversationNavigationResult> NavigateToThreadAsync(
        IInstanceSessionManager sessionManager,
        IInstanceRegistryService registry,
        IThreadRegistryService threadRegistry,
        INavigationService navigation,
        string instanceId,
        string? conversationKey,
        string? customerName,
        string? threadId = null,
        string? platform = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(threadRegistry);
        ArgumentNullException.ThrowIfNull(navigation);

        if (!ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return new ConversationNavigationResult
            {
                StatusMessage = "Invalid account selection."
            };
        }

        var instance = registry.FindById(instanceId);
        if (instance is null)
        {
            navigation.NotifyNavigationFailed(new InstanceNavigationFailedEventArgs
            {
                InstanceId = instanceId,
                ConversationKey = conversationKey,
                Message = "That account is no longer available. Refresh the dashboard and try again."
            });

            return new ConversationNavigationResult
            {
                StatusMessage = "That account is no longer available."
            };
        }

        var resolvedKey = ResolveConversationKey(
            threadRegistry,
            threadId,
            conversationKey,
            customerName,
            instanceId,
            platform ?? instance.Platform);

        navigation.OpenInstance(instanceId, resolvedKey, customerName);

        if (string.IsNullOrWhiteSpace(resolvedKey))
        {
            return new ConversationNavigationResult
            {
                InstanceOpened = true,
                ConversationFocused = false,
                StatusMessage = "Opened inbox — select the chat manually."
            };
        }

        var focusTask = ConversationFocusHelper.TryFocusConversationWithRetryAsync(
            sessionManager,
            instance,
            resolvedKey,
            customerName,
            cancellationToken);

        var completed = await Task.WhenAny(focusTask, Task.Delay(NavigationLoadingThreshold, cancellationToken))
            .ConfigureAwait(false);

        var focused = completed == focusTask && await focusTask.ConfigureAwait(false);

        if (!focused)
        {
            var message = "Opened inbox — could not focus the requested chat. Select it manually.";
            navigation.NotifyNavigationFailed(new InstanceNavigationFailedEventArgs
            {
                InstanceId = instanceId,
                ConversationKey = resolvedKey,
                Message = message
            });

            return new ConversationNavigationResult
            {
                InstanceOpened = true,
                ConversationFocused = false,
                StatusMessage = message
            };
        }

        return new ConversationNavigationResult
        {
            InstanceOpened = true,
            ConversationFocused = true
        };
    }
}
