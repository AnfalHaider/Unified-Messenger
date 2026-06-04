using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class CommandPaletteHelperTests
{
    private static CommandPaletteEntry CreateEntry(
        string title,
        string subtitle = "",
        string category = "Actions",
        CommandPaletteAction action = CommandPaletteAction.OpenDashboard) =>
        new()
        {
            Title = title,
            Subtitle = subtitle,
            Category = category,
            Selection = new CommandPaletteSelection { Action = action }
        };

    [Fact]
    public void FilterEntries_ReturnsAllEntriesForEmptyQuery()
    {
        var entries = new List<CommandPaletteEntry>
        {
            CreateEntry("Dashboard"),
            CreateEntry("Settings")
        };

        Assert.Equal(2, CommandPaletteHelper.FilterEntries(entries, null).Count);
    }

    [Fact]
    public void FilterEntries_OrdersByMatchScore()
    {
        var entries = new List<CommandPaletteEntry>
        {
            CreateEntry("WhatsApp Sales", subtitle: "Personal"),
            CreateEntry("Sales Dashboard", subtitle: "Overview"),
            CreateEntry("Settings", category: "Sales tools")
        };

        var filtered = CommandPaletteHelper.FilterEntries(entries, "sales");

        Assert.Equal("Sales Dashboard", filtered[0].Title);
        Assert.Equal("WhatsApp Sales", filtered[1].Title);
        Assert.Equal("Settings", filtered[2].Title);
    }

    [Fact]
    public void FilterEntries_CapsResultCount()
    {
        var entries = Enumerable.Range(1, 20)
            .Select(index => CreateEntry($"Item {index}"))
            .ToList();

        Assert.Equal(12, CommandPaletteHelper.FilterEntries(entries, string.Empty, maxResults: 12).Count);
    }

    [Theory]
    [InlineData(CommandPaletteAction.OpenDashboard, true)]
    [InlineData(CommandPaletteAction.OpenInstance, false)]
    public void IsValidSelection_ValidatesActionRequirements(
        CommandPaletteAction action,
        bool expected)
    {
        var selection = new CommandPaletteSelection
        {
            Action = action,
            InstanceId = action == CommandPaletteAction.OpenInstance ? "   " : null
        };

        Assert.Equal(expected, CommandPaletteHelper.IsValidSelection(selection));
    }

    [Fact]
    public void IsValidSelection_AcceptsOpenAlertWithIds()
    {
        var selection = new CommandPaletteSelection
        {
            Action = CommandPaletteAction.OpenAlert,
            AlertId = "alert-1",
            InstanceId = "inst-1"
        };

        Assert.True(CommandPaletteHelper.IsValidSelection(selection));
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    public void NormalizeQuery_TrimsInput(string query, string expected)
    {
        Assert.Equal(expected, CommandPaletteHelper.NormalizeQuery(query));
    }
}
