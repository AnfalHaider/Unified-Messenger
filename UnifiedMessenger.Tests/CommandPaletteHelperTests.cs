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

    [Fact]
    public void FilterEntries_MatchesTypoTolerantQueries()
    {
        var entries = new List<CommandPaletteEntry>
        {
            CreateEntry("Dashboard", subtitle: "Open overview", category: "Navigation"),
            CreateEntry("Settings", subtitle: "App preferences", category: "Navigation")
        };

        var filtered = CommandPaletteHelper.FilterEntries(entries, "dashbord");

        Assert.Single(filtered);
        Assert.Equal("Dashboard", filtered[0].Title);
    }

    [Fact]
    public void FilterEntries_MatchesCharacterSubsequenceQueries()
    {
        var entries = new List<CommandPaletteEntry>
        {
            CreateEntry("Toggle notification panel", subtitle: "Show or hide the hub panel", category: "Actions"),
            CreateEntry("Settings", subtitle: "App preferences", category: "Navigation")
        };

        var filtered = CommandPaletteHelper.FilterEntries(entries, "ntfpnl");

        Assert.Single(filtered);
        Assert.Equal("Toggle notification panel", filtered[0].Title);
    }

    [Fact]
    public void FilterEntries_PrefersExactMatchesOverFuzzyMatches()
    {
        var entries = new List<CommandPaletteEntry>
        {
            CreateEntry("Sales Dashboard", subtitle: "Overview"),
            CreateEntry("Dashboard", subtitle: "Open overview")
        };

        var filtered = CommandPaletteHelper.FilterEntries(entries, "dashboard");

        Assert.Equal("Dashboard", filtered[0].Title);
        Assert.Equal("Sales Dashboard", filtered[1].Title);
    }

    [Theory]
    [InlineData(CommandPaletteAction.OpenSettingsSection, "notifications", true)]
    [InlineData(CommandPaletteAction.OpenSettingsSection, " ", false)]
    public void IsValidSelection_ValidatesSettingsSectionKey(
        CommandPaletteAction action,
        string sectionKey,
        bool expected)
    {
        var selection = new CommandPaletteSelection
        {
            Action = action,
            SettingsSectionKey = sectionKey
        };

        Assert.Equal(expected, CommandPaletteHelper.IsValidSelection(selection));
    }

    [Theory]
    [InlineData("dashboard", "dashbord", true)]
    [InlineData("notification", "ntf", true)]
    [InlineData("settings", "zzzz", false)]
    public void IsCharacterSubsequence_DetectsOrderedCharacters(
        string text,
        string query,
        bool expected)
    {
        Assert.Equal(expected, CommandPaletteHelper.IsCharacterSubsequence(text, query));
    }

    [Theory]
    [InlineData("dashboard", "dashbord", 1)]
    [InlineData("settings", "setting", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void ComputeLevenshteinDistance_AllowsMinorTypos(
        string left,
        string right,
        int expectedDistance)
    {
        Assert.Equal(expectedDistance, CommandPaletteHelper.ComputeLevenshteinDistance(left, right));
    }
}
