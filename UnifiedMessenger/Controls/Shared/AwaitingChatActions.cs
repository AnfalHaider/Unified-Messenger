using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls.Shared;

/// <summary>
/// The "this one doesn't need a reply" control that sits on every awaiting-reply row, wherever such a row
/// is drawn.
/// </summary>
/// <remarks>
/// <para>
/// Some customers send the last message and simply don't need an answer ("thanks!", "ok"). Without a way to
/// close those, they sit in the backlog forever and the awaiting count stops meaning anything — so the
/// action has to be visible on the row itself, not buried.
/// </para>
/// <para>
/// It was previously an unlabelled "…" glyph on the command-center rows only, and absent entirely from the
/// per-account drill-down. This centralises it: a labelled check button whose primary action is
/// <b>Mark as done</b>, with snooze on its flyout. Both write to <see cref="AwaitingOverrideStore"/>, so
/// both self-expire — done reverts the moment a NEW customer message arrives, and a snooze runs out. The
/// backlog can be quietened, never permanently faked.
/// </para>
/// </remarks>
public static class AwaitingChatActions
{
    /// <summary>
    /// Builds the row's action control. <paramref name="onChanged"/> is raised after any override is
    /// written so the caller can re-render.
    /// </summary>
    public static FrameworkElement Build(
        string instanceId,
        OversightChatSnapshotService.ChatEntry chat,
        string displayName,
        Action onChanged,
        bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        var conversationKey = chat.ConversationKey;
        var lastActivityUtc = chat.LastActivityUtc;

        var flyout = new MenuFlyout();

        var done = new MenuFlyoutItem
        {
            Text = "Mark as done",
            Icon = new FontIcon { Glyph = "\uE73E" }
        };
        ToolTipService.SetToolTip(done,
            "Removes this conversation from Needs reply. It comes back only if the customer sends a new message.");
        done.Click += (_, _) =>
        {
            AwaitingOverrideStore.Instance.MarkHandled(instanceId, conversationKey, lastActivityUtc);
            onChanged();
        };
        flyout.Items.Add(done);
        flyout.Items.Add(new MenuFlyoutSeparator());

        void AddSnooze(string label, TimeSpan duration)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) =>
            {
                AwaitingOverrideStore.Instance.Snooze(instanceId, conversationKey, DateTimeOffset.UtcNow + duration);
                onChanged();
            };
            flyout.Items.Add(item);
        }

        AddSnooze("Snooze 1 hour", TimeSpan.FromHours(1));
        AddSnooze("Snooze 4 hours", TimeSpan.FromHours(4));
        AddSnooze("Snooze until tomorrow", TimeSpan.FromHours(Math.Max(1, 24 - DateTime.Now.Hour)));

        // A split button: the visible half does the common thing (mark done) in one click; the arrow opens
        // the snooze options. The old design put both behind an unlabelled "…", which is why the capability
        // read as missing.
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new FontIcon { Glyph = "\uE73E", FontSize = UmScale.Icon.Sm });
        if (!compact)
        {
            content.Children.Add(new TextBlock { Text = "Done", FontSize = UmScale.Icon.Sm });
        }

        var button = new SplitButton
        {
            Content = content,
            Flyout = flyout,
            Padding = new Thickness(compact ? 8 : 10, 4, compact ? 8 : 10, 4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) =>
        {
            AwaitingOverrideStore.Instance.MarkHandled(instanceId, conversationKey, lastActivityUtc);
            onChanged();
        };

        ToolTipService.SetToolTip(button,
            $"Mark {displayName} as done — no reply needed. Use the arrow to snooze instead.");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"Mark {displayName} as done");
        return button;
    }
}
