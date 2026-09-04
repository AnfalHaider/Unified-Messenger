using System.Reflection;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Keeps the tab-order bands from overlapping.
/// </summary>
/// <remarks>
/// <para><b>The bug this exists to stop happening again.</b> <c>TabIndex</c> is scoped to the whole
/// tab-navigation scope, not to the control tree the value is written in — so numbers picked
/// independently for the sidebar and for a page share one namespace. They did, and they collided: the
/// sidebar numbers every row it builds from 1 upward and a real rail carries eleven (four overview rows
/// plus seven accounts), while Settings' section nav was 10, its content 20, and the personal search box
/// 20. Sidebar row ten and the Settings nav both claimed 10. WinUI breaks ties by tree position, so tab
/// order silently stopped matching reading order — observed live as tabbing off the Dashboard row landing
/// on Reports rather than Analytics.</para>
/// <para>Nothing else catches this. Every value is individually reasonable; only their relationship is
/// wrong, and a reviewer looking at one file sees nothing amiss.</para>
/// </remarks>
public class AccessibilityTabOrderTests
{
    private static IReadOnlyDictionary<string, int> Constants() =>
        typeof(AccessibilityTabOrderHelper)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);

    [Fact]
    public void TheSidebarBandCannotReachTheFooter() =>
        Assert.True(
            AccessibilityTabOrderHelper.SidebarRowCeiling < AccessibilityTabOrderHelper.SidebarFooterAddInstance,
            "A sidebar row could take the Add-account button's index.");

    [Fact]
    public void PageContentSitsAboveEverySidebarIndex()
    {
        // The whole point of the bands. Any page constant at or below the footer is back in the sidebar's
        // numbering and will collide as soon as the owner adds enough accounts.
        var footerTop = AccessibilityTabOrderHelper.SidebarFooterSettings;

        foreach (var (name, value) in Constants().Where(c => c.Key.StartsWith("Settings", StringComparison.Ordinal)
                                                          || c.Key.StartsWith("Personal", StringComparison.Ordinal)))
        {
            Assert.True(
                value > footerTop,
                $"{name} is {value}, inside the sidebar/footer range (<= {footerTop}). Page content starts at "
                + $"{AccessibilityTabOrderHelper.PageContentBase}.");
        }
    }

    [Fact]
    public void NoTwoConstantsShareAnIndex()
    {
        var byValue = Constants()
            .Where(c => c.Key != nameof(AccessibilityTabOrderHelper.PageContentBase)
                     && c.Key != nameof(AccessibilityTabOrderHelper.SidebarRowCeiling))
            .GroupBy(c => c.Value)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Key))}")
            .ToList();

        Assert.True(byValue.Count == 0, "Constants share a tab index — " + string.Join(" · ", byValue));
    }

    [Fact]
    public void TheSidebarFooterLiteralsInXamlStillMatchTheConstants()
    {
        // WorkspaceSidebar.xaml writes 90/91/92 as literals rather than binding the constants, and the
        // helper's own comment records that these two HAD already drifted once — Notifications carrying
        // Settings' number, and Settings carrying one that appeared nowhere in the helper at all.
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "UnifiedMessenger", "Controls", "WorkspaceSidebar.xaml"));

        Assert.Contains($"TabIndex=\"{AccessibilityTabOrderHelper.SidebarFooterAddInstance}\"", xaml, StringComparison.Ordinal);
        Assert.Contains($"TabIndex=\"{AccessibilityTabOrderHelper.SidebarFooterNotifications}\"", xaml, StringComparison.Ordinal);
        Assert.Contains($"TabIndex=\"{AccessibilityTabOrderHelper.SidebarFooterSettings}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePersonalPanelNoLongerCarriesItsOwnLiterals()
    {
        // Its 15 and 20 were exactly the values that collided with sidebar rows fifteen and twenty. Both
        // now come from the constants in code-behind, so there is one place to keep in step instead of two.
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "UnifiedMessenger", "Controls", "PersonalOverviewPanel.xaml"));

        Assert.DoesNotContain("TabIndex=", xaml, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnifiedMessenger.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
