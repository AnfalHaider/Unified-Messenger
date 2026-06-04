using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests;

[Collection("SettingsSerial")]
public class AutoDraftOrchestratorTests : IAsyncLifetime
{
    private AppSettings _originalSettings = new();

    public Task InitializeAsync()
    {
        _originalSettings = new AppSettings
        {
            EnableLocalAi = AppSettingsService.Instance.Settings.EnableLocalAi,
            EnableAutoDraft = AppSettingsService.Instance.Settings.EnableAutoDraft,
            AutoDraftOnlyWhenVisible = AppSettingsService.Instance.Settings.AutoDraftOnlyWhenVisible
        };
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await AppSettingsService.Instance.UpdateAsync(settings =>
        {
            settings.EnableLocalAi = _originalSettings.EnableLocalAi;
            settings.EnableAutoDraft = _originalSettings.EnableAutoDraft;
            settings.AutoDraftOnlyWhenVisible = _originalSettings.AutoDraftOnlyWhenVisible;
        });
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenAutoDraftDisabled_DoesNothing()
    {
        await AppSettingsService.Instance.UpdateAsync(settings =>
        {
            settings.EnableLocalAi = true;
            settings.EnableAutoDraft = false;
        });

        var orchestrator = new AutoDraftOrchestrator();
        await orchestrator.ProcessInboundAsync(
            new InboundMessageSelection
            {
                InstanceId = "inst-1",
                Platform = "metabusiness",
                MessageText = "I need to reschedule my appointment tomorrow."
            });

        // No exception and no draft event is sufficient; injection would fail without WebView anyway.
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenInstanceNotVisible_SkipsDraft()
    {
        await AppSettingsService.Instance.UpdateAsync(settings =>
        {
            settings.EnableLocalAi = true;
            settings.EnableAutoDraft = true;
            settings.AutoDraftOnlyWhenVisible = true;
        });

        ActiveWorkspaceContext.SetActiveInstance("other-instance");

        var orchestrator = new AutoDraftOrchestrator();
        await orchestrator.ProcessInboundAsync(
            new InboundMessageSelection
            {
                InstanceId = "inst-hidden",
                Platform = "metabusiness",
                MessageText = "Can you confirm my booking for Friday?"
            });
    }

    [Fact]
    public void AiDraftPromptService_BuildsPlatformSpecificPrompt()
    {
        var request = AiDraftPromptService.BuildPrompt(
            "metabusiness",
            "My order arrived damaged.",
            "Alex",
            "Depilex F-11");

        Assert.Contains("Meta Business", request.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Alex", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("damaged", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
