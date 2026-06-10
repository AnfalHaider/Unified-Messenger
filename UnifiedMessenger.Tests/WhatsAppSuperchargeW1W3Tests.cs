using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WhatsAppSuperchargeW1W3Tests
{
    private static void ConfigureWhatsAppOnlyModules(AppSettings settings)
    {
        PlatformModuleSettingsHelper.NormalizePlatformModules(settings);
        foreach (var item in settings.PlatformModules)
        {
            item.IsEnabled = item.PlatformId is "whatsapp" or "whatsappbusiness";
        }
    }

    [Fact]
    public void WhatsAppFocusPreset_EmphasizesOperationalPanels()
    {
        var placements = OccLayoutPresets.CreateWhatsAppFocus();

        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.ImmediateLanePanelId && placement.MinHeightDp >= 280);
        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.KanbanPanelId && placement.MinHeightDp >= 360);
        Assert.DoesNotContain(placements, placement =>
            placement.PanelId == OccLayoutDefaults.PlatformIntelligencePanelId);
        Assert.DoesNotContain(placements, placement =>
            placement.PanelId == OccLayoutDefaults.AnalyticsPanelId);
    }

    [Fact]
    public void IsWhatsAppOnlyMode_TrueWhenOnlyWhatsAppModulesEnabled()
    {
        var settings = new AppSettings();
        ConfigureWhatsAppOnlyModules(settings);

        Assert.True(WhatsAppFocusLayoutHelper.IsWhatsAppOnlyMode(settings));
    }

    [Fact]
    public void IsWhatsAppOnlyMode_FalseWhenOtherPlatformsEnabled()
    {
        var settings = new AppSettings();
        ConfigureWhatsAppOnlyModules(settings);
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "telegram", isEnabled: true);

        Assert.False(WhatsAppFocusLayoutHelper.IsWhatsAppOnlyMode(settings));
    }

    [Fact]
    public void TryApplyRecommendedLayout_AppliesOnceForWhatsAppOnlyMode()
    {
        var settings = new AppSettings();
        ConfigureWhatsAppOnlyModules(settings);

        Assert.True(WhatsAppFocusLayoutHelper.TryApplyRecommendedLayout(settings));
        Assert.Equal(OccLayoutPresets.WhatsAppFocus, settings.OccLayoutPresetId);
        Assert.Equal(WhatsAppFocusLayoutHelper.WhatsAppFocusKpiOrder, settings.OccKpiMetricOrder);
        Assert.Equal(WhatsAppFocusLayoutHelper.WhatsAppFocusHiddenPanels, settings.OccHiddenPanels);
        Assert.True(settings.OccWhatsAppFocusLayoutApplied);

        Assert.False(WhatsAppFocusLayoutHelper.TryApplyRecommendedLayout(settings));
    }

    [Fact]
    public void TryParseResponse_AcceptsEnrichedWhatsAppSchema()
    {
        const string json = """
            {
              "customerIntent": "BookingRequest",
              "intentConfidence": 0.92,
              "subIntent": "BridalUrgent",
              "tags": ["urgent", "bridal"],
              "requestedServices": ["Bridal Makeup"],
              "branchTarget": "DHA-2",
              "sentiment": "Frustrated",
              "actionableSummary": "Hold Saturday bridal slot and request deposit.",
              "suggestedDraftResponse": "Saturday bridal slot available — shall I hold with 50% advance?"
            }
            """;

        var parsed = MessageTriageInferenceRunner.TryParseResponse(json, out var response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Equal("BridalUrgent", response!.SubIntent);
        Assert.Equal(0.92, response.IntentConfidence);
        Assert.Equal(2, response.Tags.Count);
        Assert.Equal(5, response.OperationalUrgency);
    }

    [Fact]
    public void ApplyWhatsAppSubIntentBoost_RaisesDepositQueryUrgency()
    {
        var response = new RichTriageLlmResponse
        {
            SubIntent = "DepositQuery",
            OperationalUrgency = 1
        };

        var boosted = MessageTriageInferenceRunner.ApplyWhatsAppSubIntentBoost(response, 1);

        Assert.Equal(3, boosted);
    }

    [Fact]
    public void CopilotPrompt_IncludesOperationalContextForWhatsApp()
    {
        var prompt = AiWhatsAppCopilotPromptService.BuildPrompt(
            "Depilex DHA-2",
            "DHA-2",
            "Sara",
            "Need bridal slot Saturday",
            [
                new ConversationMessageEntry { Direction = "incoming", Text = "Need bridal slot Saturday" },
                new ConversationMessageEntry { Direction = "outgoing", Text = "Welcome to Depilex" }
            ],
            new WhatsAppConversationMetadata
            {
                BusinessLabels = ["VIP"],
                VerifiedBusinessName = "Depilex Salon"
            },
            "wa-1",
            "120363@s.whatsapp.net");

        Assert.Contains("BRANCH OPERATIONS", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Bridal Makeup", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Recent transcript:", prompt.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Draft tone:", prompt.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SidebarPlan_UsesFlatListInWhatsAppOnlyMode()
    {
        var original = AppSettingsService.Instance.Settings.PlatformModules.ToList();
        try
        {
            ConfigureWhatsAppOnlyModules(AppSettingsService.Instance.Settings);

            var instances = new[]
            {
                new MessengerInstance
                {
                    Id = "pro-1",
                    DisplayName = "Depilex DHA-2",
                    Platform = "whatsappbusiness",
                    ProfileName = "pro-1",
                    Category = WorkspaceCategory.Professional
                },
                new MessengerInstance
                {
                    Id = "personal-1",
                    DisplayName = "Personal WA",
                    Platform = "whatsapp",
                    ProfileName = "personal-1",
                    Category = WorkspaceCategory.Personal
                }
            };

            var plan = WorkspaceSidebarMenuPlanner.BuildPlan(instances);

            Assert.DoesNotContain(plan.Entries, entry => entry.Key == WorkspaceSidebarMenuPlanner.ProfessionalHeaderKey);
            Assert.DoesNotContain(plan.Entries, entry => entry.Key == WorkspaceSidebarMenuPlanner.PersonalHeaderKey);
            Assert.Equal(4, plan.Entries.Count);
        }
        finally
        {
            AppSettingsService.Instance.Settings.PlatformModules = original;
            PlatformModuleSettingsHelper.NormalizePlatformModules(AppSettingsService.Instance.Settings);
        }
    }
}
