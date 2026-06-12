using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Shell;

public sealed class ShellCommandPaletteCoordinator
{
    private readonly ApplicationServices _services;

    public ShellCommandPaletteCoordinator(ApplicationServices services)
    {
        _services = services;
    }

    public IReadOnlyList<CommandPaletteEntry> BuildEntries()
    {
        var entries = new List<CommandPaletteEntry>
        {
            new()
            {
                Title = "Dashboard",
                Subtitle = "Open overview",
                Category = "Navigation",
                IconGlyph = "\uE80F",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.OpenDashboard }
            },
            new()
            {
                Title = "Settings",
                Subtitle = "App preferences",
                Category = "Navigation",
                IconGlyph = "\uE713",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.OpenSettings }
            },
            new()
            {
                Title = "Settings: Notifications",
                Subtitle = "Toast, badge, and panel preferences",
                Category = "Navigation",
                IconGlyph = "\uE713",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenSettingsSection,
                    SettingsSectionKey = SettingsNavigationHelper.NotificationsSectionKey
                }
            },
            new()
            {
                Title = "Settings: Updates",
                Subtitle = "Auto-update and version checks",
                Category = "Navigation",
                IconGlyph = "\uE713",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenSettingsSection,
                    SettingsSectionKey = SettingsNavigationHelper.SystemSectionKey
                }
            },
            new()
            {
                Title = "Toggle notification panel",
                Subtitle = "Show or hide the hub panel",
                Category = "Actions",
                IconGlyph = "\uEA8F",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.ToggleNotifications }
            },
            new()
            {
                Title = "Mark all notifications read",
                Subtitle = "Clear unread state in the hub",
                Category = "Actions",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.MarkAllRead }
            },
            new()
            {
                Title = "Clear notification history",
                Subtitle = "Remove all hub alerts",
                Category = "Actions",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.ClearNotifications }
            },
            new()
            {
                Title = "Refresh Operations Command Center",
                Subtitle = "Reload KPIs, kanban, and insights",
                Category = "Operations",
                IconGlyph = "\uE72C",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.RefreshOcc }
            },
            new()
            {
                Title = "Open immediate action queue",
                Subtitle = "Jump to urgent threads on the dashboard",
                Category = "Operations",
                IconGlyph = "\uE7BA",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.OpenImmediateQueue }
            }
        };

        var professionalInstances = _services.Registry.Instances.Where(i => i.IsProfessional).ToList();
        var branchKeys = BranchWorkspaceHelper.CollectBranchKeys(
            professionalInstances,
            _services.ThreadRegistry.GetAllThreads());
        foreach (var branchKey in branchKeys)
        {
            entries.Add(new CommandPaletteEntry
            {
                Title = $"Filter branch: {branchKey}",
                Subtitle = "Scope OCC to this branch workspace",
                Category = "Operations",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.FilterBranch,
                    BranchKey = branchKey
                }
            });
        }

        foreach (var instance in _services.Registry.GetOrderedInstances())
        {
            var platform = PlatformDefinition.FindById(instance.Platform);
            entries.Add(new CommandPaletteEntry
            {
                Title = instance.DisplayName,
                Subtitle = $"{platform?.DisplayName ?? instance.Platform} · {instance.Category}",
                Category = "Instances",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenInstance,
                    InstanceId = instance.Id
                }
            });
        }

        foreach (var alert in _services.NotificationHub.GetAlertsSortedByInstance().Take(20))
        {
            entries.Add(new CommandPaletteEntry
            {
                Title = alert.Title,
                Subtitle = $"{alert.InstanceDisplayName} · {alert.Body}",
                Category = "Notifications",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenAlert,
                    InstanceId = alert.InstanceId,
                    AlertId = alert.Id
                }
            });
        }

        return entries;
    }

    public async Task<bool> ConfirmClearNotificationsAsync()
    {
        if (_services.NotificationHub.Alerts.Count == 0)
        {
            return false;
        }

        var confirmed = await _services.Dialog.ConfirmAsync(
            "Clear all notifications?",
            "This removes every alert from the notification panel and resets unread sidebar badges.",
            "Clear all");

        if (confirmed)
        {
            _services.NotificationHub.ClearAlerts();
        }

        return confirmed;
    }
}
