using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DeleteInstanceDialogHelperTests
{
    [Theory]
    [InlineData(null, "this account")]
    [InlineData("   ", "this account")]
    [InlineData("  Work WhatsApp  ", "Work WhatsApp")]
    public void NormalizeDisplayName_TrimsAndFallsBack(string? displayName, string expected)
    {
        Assert.Equal(expected, DeleteInstanceDialogHelper.NormalizeDisplayName(displayName));
    }

    [Fact]
    public void BuildDescription_QuotesNormalizedDisplayName()
    {
        Assert.Equal(
            "How would you like to remove \"Sales\"?",
            DeleteInstanceDialogHelper.BuildDescription("  Sales  "));
    }

    [Theory]
    [InlineData(DeleteInstanceChoice.Cancelled, false, false, false)]
    [InlineData(DeleteInstanceChoice.RemoveFromSidebar, true, true, false)]
    [InlineData(DeleteInstanceChoice.PermanentDelete, true, false, true)]
    public void ChoiceHelpers_ClassifyDeleteActions(
        DeleteInstanceChoice choice,
        bool wasConfirmed,
        bool shouldArchive,
        bool isDestructive)
    {
        Assert.Equal(wasConfirmed, DeleteInstanceDialogHelper.WasConfirmed(choice));
        Assert.Equal(shouldArchive, DeleteInstanceDialogHelper.ShouldArchiveInstance(choice));
        Assert.Equal(isDestructive, DeleteInstanceDialogHelper.IsDestructiveChoice(choice));
    }
}
