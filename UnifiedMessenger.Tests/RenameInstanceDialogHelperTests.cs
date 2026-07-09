using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class RenameInstanceDialogHelperTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("  Work  ", "Work")]
    public void NormalizeInitialDisplayName_TrimsAndHandlesBlank(string? currentName, string expected)
    {
        Assert.Equal(expected, RenameInstanceDialogHelper.NormalizeInitialDisplayName(currentName));
    }

    [Fact]
    public void ValidatePrimaryAction_RejectsBlankName()
    {
        var submission = RenameInstanceDialogHelper.ValidatePrimaryAction("Work", "   ");

        Assert.False(submission.IsValid);
        Assert.Equal(RenameInstanceDialogHelper.RequiredDisplayNameMessage, submission.ValidationMessage);
    }

    [Fact]
    public void ValidatePrimaryAction_NormalizesEditedName()
    {
        var submission = RenameInstanceDialogHelper.ValidatePrimaryAction("Work", "  Sales Desk  ");

        Assert.True(submission.IsValid);
        Assert.Equal("Sales Desk", submission.DisplayName);
        Assert.False(submission.IsUnchanged);
    }

    [Fact]
    public void ValidatePrimaryAction_DetectsUnchangedName()
    {
        var submission = RenameInstanceDialogHelper.ValidatePrimaryAction("Work", " Work ");

        Assert.True(submission.IsValid);
        Assert.True(submission.IsUnchanged);
    }
}
