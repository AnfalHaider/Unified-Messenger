namespace UnifiedMessenger.Models;

/// <summary>
/// Single source of truth for which shell destination is active.
/// </summary>
public enum ShellViewState
{
    Dashboard,

    Settings,

    Instance,

    /// <summary>
    /// Notification hub panel is open; underlying page state is preserved.
    /// </summary>
    NotificationHub,

    /// <summary>
    /// Work queue page is active.
    /// </summary>
    WorkQueue
}
