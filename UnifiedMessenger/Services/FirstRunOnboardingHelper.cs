using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// First-run dialog explaining Personal vs Professional workspace instances.
/// </summary>
public static class FirstRunOnboardingHelper
{
    public static async Task<bool> TryShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var dialog = new ContentDialog
        {
            Title = "Welcome to Unified Messenger",
            PrimaryButtonText = "Got it",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Content = BuildContent()
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static StackPanel BuildContent()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = "Each WhatsApp account runs in its own isolated WebView profile.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Personal — quick access, notifications, and unread badges for everyday messaging.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Professional — Operations Command Center with triage, branch workspaces, SLA tracking, and kanban queues for business inboxes.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Choose the workspace when adding an instance. You can change it later from the sidebar context menu.",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return panel;
    }
}
