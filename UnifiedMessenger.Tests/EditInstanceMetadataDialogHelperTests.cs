using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class EditInstanceMetadataDialogHelperTests
{
    [Fact]
    public void CreateInitialFormState_NormalizesPersistedValues()
    {
        var instance = new MessengerInstance
        {
            DisplayName = "  Work  ",
            Platform = "whatsapp",
            StartUrl = " https://web.whatsapp.com ",
            Notes = "  desk phone  "
        };

        var state = EditInstanceMetadataDialogHelper.CreateInitialFormState(instance);

        Assert.Equal("Work", state.DisplayName);
        Assert.Equal("whatsapp", state.PlatformId);
        Assert.Equal("https://web.whatsapp.com", state.StartUrl);
        Assert.Equal("desk phone", state.Notes);
        Assert.Equal(string.Empty, state.BranchKey);
    }

    [Fact]
    public void ValidatePrimaryAction_PersistsExplicitBranchKey()
    {
        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Inbox",
            Platform = "metabusiness",
            StartUrl = "https://business.facebook.com"
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "Inbox",
            PlatformDefinition.FindById("metabusiness"),
            customUrlText: "https://business.facebook.com",
            notesText: null,
            branchKeyText: "DHA-2");

        Assert.True(submission.IsValid);
        Assert.Equal("DHA-2", submission.BranchKey);
    }

    [Fact]
    public void ValidatePrimaryAction_RequiresDisplayName()
    {
        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Work",
            Platform = "whatsapp",
            StartUrl = "https://web.whatsapp.com"
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "   ",
            PlatformDefinition.FindById("whatsapp"),
            customUrlText: initial.StartUrl,
            notesText: null);

        Assert.False(submission.IsValid);
        Assert.Equal(EditInstanceMetadataDialogHelper.RequiredDisplayNameMessage, submission.ValidationMessage);
    }

    [Fact]
    public void ValidatePrimaryAction_RequiresStartUrlForGenericPlatform()
    {
        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Custom",
            Platform = "generic",
            StartUrl = "https://example.com"
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "Custom",
            PlatformDefinition.FindById("generic"),
            customUrlText: null,
            notesText: null);

        Assert.False(submission.IsValid);
        Assert.Equal(EditInstanceMetadataDialogHelper.RequiredStartUrlMessage, submission.ValidationMessage);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    public void ValidatePrimaryAction_RejectsInvalidStartUrl(string url)
    {
        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Custom",
            Platform = "generic",
            StartUrl = "https://example.com"
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "Custom",
            PlatformDefinition.FindById("generic"),
            customUrlText: url,
            notesText: null);

        Assert.False(submission.IsValid);
        Assert.Equal(EditInstanceMetadataDialogHelper.InvalidStartUrlMessage, submission.ValidationMessage);
    }

    [Fact]
    public void ValidatePrimaryAction_UsesPlatformDefaultWhenUrlBlank()
    {
        var whatsapp = PlatformDefinition.FindById("whatsapp");
        Assert.NotNull(whatsapp);

        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Work",
            Platform = "whatsapp",
            StartUrl = whatsapp.DefaultUrl
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "Work",
            whatsapp,
            customUrlText: null,
            notesText: null);

        Assert.True(submission.IsValid);
        Assert.Equal(whatsapp.DefaultUrl, submission.StartUrl);
        Assert.True(submission.IsUnchanged);
    }

    [Fact]
    public void TryResolveCustomUrlPlaceholder_UsesDefaultForKnownPlatform()
    {
        var whatsapp = PlatformDefinition.FindById("whatsapp");
        Assert.NotNull(whatsapp);

        var placeholder = EditInstanceMetadataDialogHelper.TryResolveCustomUrlPlaceholder(
            whatsapp,
            currentUrlText: null,
            originalStartUrl: whatsapp.DefaultUrl);

        Assert.Equal(whatsapp.DefaultUrl, placeholder);
    }

    [Fact]
    public void ValidatePrimaryAction_NormalizesNotesAndDetectsChanges()
    {
        var initial = EditInstanceMetadataDialogHelper.CreateInitialFormState(new MessengerInstance
        {
            DisplayName = "Work",
            Platform = "whatsapp",
            StartUrl = "https://web.whatsapp.com"
        });

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            initial,
            displayName: "  Sales  ",
            PlatformDefinition.FindById("whatsapp"),
            customUrlText: "https://web.whatsapp.com",
            notesText: "  follow up  ");

        Assert.True(submission.IsValid);
        Assert.Equal("Sales", submission.DisplayName);
        Assert.Equal("follow up", submission.Notes);
        Assert.False(submission.IsUnchanged);
    }
}
