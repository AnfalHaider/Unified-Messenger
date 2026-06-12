using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AddInstanceDialogHelperTests
{
    [Theory]
    [InlineData("whatsapp", false)]
    [InlineData("whatsappbusiness", false)]
    public void ShouldShowCustomUrlField_IsFalseForWhatsAppPlatforms(string platformId, bool expected)
    {
        Assert.Equal(expected, AddInstanceDialogHelper.ShouldShowCustomUrlField(platformId));
    }

    [Fact]
    public void ValidatePrimaryAction_AcceptsRestoreSelection()
    {
        var submission = AddInstanceDialogHelper.ValidatePrimaryAction(
            new MessengerInstance { Id = " archived-1 ", DisplayName = "Work" },
            displayName: null,
            platform: null,
            customUrl: null,
            WorkspaceCategory.Personal);

        Assert.True(submission.IsValid);
        Assert.Equal("archived-1", submission.RestoreInstanceId);
    }

    [Fact]
    public void ValidatePrimaryAction_RequiresDisplayNameForNewInstance()
    {
        var submission = AddInstanceDialogHelper.ValidatePrimaryAction(
            selectedArchivedInstance: null,
            displayName: "   ",
            platform: PlatformDefinition.All[0],
            customUrl: null,
            WorkspaceCategory.Personal);

        Assert.False(submission.IsValid);
        Assert.Equal("Display name is required.", submission.ValidationMessage);
    }

    [Theory]
    [InlineData("ftp://example.com", false)]
    [InlineData("https://example.com", true)]
    public void TryNormalizeCustomUrl_ValidatesAbsoluteHttpUrls(string url, bool expectedValid)
    {
        var isValid = AddInstanceDialogHelper.TryNormalizeCustomUrl(url, out _, out var errorMessage);

        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedValid, errorMessage is null);
    }

    [Fact]
    public void ValidatePrimaryAction_NormalizesNewInstanceFields()
    {
        var submission = AddInstanceDialogHelper.ValidatePrimaryAction(
            selectedArchivedInstance: null,
            displayName: "  Sales  ",
            platform: PlatformDefinition.FindById("whatsapp"),
            customUrl: null,
            WorkspaceCategory.Professional);

        Assert.True(submission.IsValid);
        Assert.Equal("Sales", submission.DisplayName);
        Assert.Equal("whatsapp", submission.PlatformId);
        Assert.Equal(WorkspaceCategory.Professional, submission.Category);
    }
}
