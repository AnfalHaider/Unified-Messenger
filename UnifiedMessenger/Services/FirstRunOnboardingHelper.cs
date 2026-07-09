using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Result of the first-run onboarding wizard indicating which follow-up actions the user opted into.
/// </summary>
public sealed record OnboardingWizardResult(
    bool AddAccount,
    bool ConfigureLocations,
    bool ConfigureHoursSla,
    bool SetupAi,
    bool WasSkipped);

/// <summary>
/// Guided 4-step first-run onboarding wizard.
/// Steps: Welcome → Add account → Set locations → Configure hours/SLA → (optional) AI
/// Each step has an individual skip button; the entire wizard can be dismissed at any time.
/// </summary>
public static class FirstRunOnboardingHelper
{
    private const int TotalSteps = 4;

    public static async Task<OnboardingWizardResult> TryShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var state = new WizardState();
        var dialog = BuildDialog(xamlRoot, state);

        await dialog.ShowAsync();
        return state.BuildResult();
    }

    private static ContentDialog BuildDialog(XamlRoot xamlRoot, WizardState state)
    {
        var dialog = new ContentDialog
        {
            Title = BuildTitle(1),
            PrimaryButtonText = "Next",
            SecondaryButtonText = "Skip this step",
            CloseButtonText = "Skip setup",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        // Start on step 1. Each primary click advances; secondary skips current step.
        dialog.Content = BuildStepContent(1, state);
        state.CurrentStep = 1;

        dialog.PrimaryButtonClick += (sender, args) =>
        {
            args.Cancel = true;
            AdvanceStep(sender, state);
        };

        dialog.SecondaryButtonClick += (sender, args) =>
        {
            args.Cancel = true;
            SkipCurrentStep(sender, state);
        };

        dialog.CloseButtonClick += (_, _) =>
        {
            state.WasSkipped = true;
        };

        return dialog;
    }

    private static void AdvanceStep(ContentDialog dialog, WizardState state)
    {
        // Record that the user actively chose the current step.
        RecordStepChoice(state, state.CurrentStep, chosen: true);

        if (state.CurrentStep >= TotalSteps)
        {
            dialog.Hide();
            return;
        }

        state.CurrentStep++;
        UpdateDialog(dialog, state);
    }

    private static void SkipCurrentStep(ContentDialog dialog, WizardState state)
    {
        RecordStepChoice(state, state.CurrentStep, chosen: false);

        if (state.CurrentStep >= TotalSteps)
        {
            dialog.Hide();
            return;
        }

        state.CurrentStep++;
        UpdateDialog(dialog, state);
    }

    private static void RecordStepChoice(WizardState state, int step, bool chosen)
    {
        switch (step)
        {
            case 2:
                state.AddAccount = chosen;
                break;
            case 3:
                state.ConfigureLocations = chosen;
                break;
            case 4:
                state.ConfigureHoursSla = chosen;
                break;
        }
    }

    private static void UpdateDialog(ContentDialog dialog, WizardState state)
    {
        dialog.Title = BuildTitle(state.CurrentStep);
        dialog.Content = BuildStepContent(state.CurrentStep, state);

        var isLastStep = state.CurrentStep >= TotalSteps;
        dialog.PrimaryButtonText = isLastStep ? "Finish" : "Next";
        dialog.SecondaryButtonText = "Skip this step";
        dialog.DefaultButton = ContentDialogButton.Primary;
    }

    private static string BuildTitle(int step) => step switch
    {
        1 => "Welcome to Unified Messenger",
        2 => $"Step 1 of {TotalSteps - 1} — Add an account",
        3 => $"Step 2 of {TotalSteps - 1} — Set up locations",
        4 => $"Step 3 of {TotalSteps - 1} — Configure hours and SLA",
        _ => "Getting started"
    };

    private static FrameworkElement BuildStepContent(int step, WizardState state) => step switch
    {
        1 => BuildWelcomeStep(),
        2 => BuildAddAccountStep(),
        3 => BuildLocationsStep(),
        4 => BuildHoursSlaStep(),
        _ => BuildWelcomeStep()
    };

    private static StackPanel BuildWelcomeStep()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = "Each WhatsApp account runs in its own isolated WebView profile — your sessions, cookies, and local data are completely separated.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Personal accounts give you quick access, notifications, and unread badges for everyday messaging.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Professional accounts unlock the Operations Command Center — triage queues, branch workspaces, SLA tracking, and kanban boards for business inboxes.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "This short setup guide will walk you through adding your first account and configuring your workspace. You can skip any step.",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return panel;
    }

    private static StackPanel BuildAddAccountStep()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = "Add your first messaging account to start receiving unified notifications.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Click Next and an Add Account dialog will open automatically. You can add more accounts at any time from the sidebar.",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Choose Personal for everyday accounts or Professional for business inboxes — you can change this later from the sidebar context menu.",
            FontSize = 12,
            Opacity = 0.65,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return panel;
    }

    private static StackPanel BuildLocationsStep()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = "Locations group professional accounts for branch-level analytics and filtering.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "For example, multiple WhatsApp Business accounts in the same city can be grouped under a \"London\" or \"Karachi\" location — the Work Queue and Command Center will roll them up together.",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Click Next to open the Workspace Manager where you can assign locations and configure per-location SLA thresholds.",
            FontSize = 12,
            Opacity = 0.65,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return panel;
    }

    private static StackPanel BuildHoursSlaStep()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = "Set operating hours and SLA response targets for your professional inboxes.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "The Operations Command Center tracks how quickly your team replies to customer messages. Set a threshold (e.g. 30 minutes) and it will flag breaches in the Work Queue.",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Operating hours control when the breach timer runs — messages received outside your defined hours won't count against your SLA.",
            FontSize = 12,
            Opacity = 0.65,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Click Finish to open the Workspace Manager and configure these settings now.",
            FontSize = 12,
            Opacity = 0.65,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return panel;
    }

    private sealed class WizardState
    {
        public int CurrentStep { get; set; } = 1;
        public bool AddAccount { get; set; }
        public bool ConfigureLocations { get; set; }
        public bool ConfigureHoursSla { get; set; }
        public bool WasSkipped { get; set; }

        public OnboardingWizardResult BuildResult() => new(
            AddAccount: AddAccount,
            ConfigureLocations: ConfigureLocations,
            ConfigureHoursSla: ConfigureHoursSla,
            SetupAi: false,
            WasSkipped: WasSkipped);
    }
}
