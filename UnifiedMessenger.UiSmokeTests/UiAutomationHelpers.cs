using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

internal static class UiAutomationHelpers
{
    public static bool WaitForMarker(AutomationElement root, string marker, TimeSpan timeout) =>
        WaitForMarkerOrAutomationId(root, marker, null, timeout);

    public static bool WaitForMarkerOrAutomationId(
        AutomationElement root,
        string name,
        string? automationId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindByName(root, name) is not null ||
                (!string.IsNullOrWhiteSpace(automationId) && FindByAutomationId(root, automationId) is not null))
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    public static AutomationElement? FindByName(AutomationElement root, string name)
    {
        try
        {
            return root.FindFirstDescendant(root.ConditionFactory.ByName(name));
        }
        catch
        {
            return null;
        }
    }

    public static AutomationElement? FindByAutomationId(AutomationElement root, string automationId)
    {
        try
        {
            return root.FindFirstDescendant(root.ConditionFactory.ByAutomationId(automationId));
        }
        catch
        {
            return null;
        }
    }

    public static AutomationElement? FindByNameContains(AutomationElement root, string fragment)
    {
        foreach (var controlType in new[] { ControlType.TabItem, ControlType.Button, ControlType.Text, ControlType.Pane })
        {
            foreach (var candidate in root.FindAllDescendants(root.ConditionFactory.ByControlType(controlType)))
            {
                if (NameContains(candidate, fragment))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static bool ClickByName(AutomationElement root, string name)
    {
        var target = FindByName(root, name);
        return target is not null && ClickElement(target);
    }

    public static bool ClickByNameOrAutomationId(
        AutomationElement root,
        string name,
        string? automationId = null)
    {
        if (ClickByName(root, name))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(automationId))
        {
            return false;
        }

        var target = FindByAutomationId(root, automationId);
        return target is not null && ClickElement(target);
    }

    public static bool FindMarkerOrAutomationId(
        AutomationElement root,
        string name,
        string? automationId = null) =>
        FindByName(root, name) is not null ||
        (!string.IsNullOrWhiteSpace(automationId) && FindByAutomationId(root, automationId) is not null);

    public static void FocusWindow(AutomationElement window)
    {
        try
        {
            window.Focus();
            window.SetForeground();
        }
        catch
        {
            try
            {
                window.Focus();
            }
            catch
            {
                // UIA tree may be stale after navigation; callers should retry.
            }
        }
    }

    public static void SendChord(VirtualKeyShort modifier, VirtualKeyShort key)
    {
        Keyboard.Press(modifier);
        Keyboard.Press(key);
        Keyboard.Release(key);
        Keyboard.Release(modifier);
    }

    public static void SendEscape() => Keyboard.Press(VirtualKeyShort.ESCAPE);

    public static IReadOnlyList<string> SampleNames(AutomationElement root, int max = 20) =>
        SafeTextNames(root, max: max);

    /// <summary>Enumerate Text control names without throwing on unsupported UIA Name property.</summary>
    public static IReadOnlyList<string> SafeTextNames(
        AutomationElement root,
        Func<string, bool>? predicate = null,
        int max = 20)
    {
        var results = new List<string>();
        try
        {
            foreach (var element in root.FindAllDescendants(root.ConditionFactory.ByControlType(ControlType.Text)))
            {
                var name = SafeName(element);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (predicate is not null && !predicate(name))
                {
                    continue;
                }

                if (results.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(name);
                if (results.Count >= max)
                {
                    break;
                }
            }
        }
        catch
        {
            // Stale UIA tree during navigation.
        }

        return results;
    }

    public static string? SafeAutomationId(AutomationElement element)
    {
        try
        {
            return element.AutomationId;
        }
        catch
        {
            return null;
        }
    }

    public static bool SafeIsEnabled(AutomationElement element)
    {
        try
        {
            return element.IsEnabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enumerate descendants without aborting when individual nodes throw.</summary>
    public static IEnumerable<AutomationElement> SafeDescendants(
        AutomationElement root,
        ControlType controlType)
    {
        AutomationElement[] elements;
        try
        {
            elements = root.FindAllDescendants(root.ConditionFactory.ByControlType(controlType));
        }
        catch
        {
            yield break;
        }

        foreach (var element in elements)
        {
            yield return element;
        }
    }

    public static AutomationElement? FindDialog(AutomationElement scope, string titleFragment)
    {
        foreach (var candidate in scope.FindAllDescendants(scope.ConditionFactory.ByControlType(ControlType.Window)))
        {
            if (NameContains(candidate, titleFragment))
            {
                return candidate;
            }
        }

        foreach (var candidate in scope.FindAllDescendants(scope.ConditionFactory.ByControlType(ControlType.Pane)))
        {
            if (NameContains(candidate, titleFragment))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool ClickByNameContains(AutomationElement root, string fragment)
    {
        var target = FindByNameContains(root, fragment);
        return target is not null && ClickElement(target);
    }

    public static bool EnsureDashboardOperationsTab(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        if (ClickByNameOrAutomationId(window, "Operations Command Center Tab", "DashboardOccTab"))
        {
            Thread.Sleep(400);
            return true;
        }

        foreach (var tab in window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.TabItem)))
        {
            try
            {
                if (NameContains(tab, "Operations Command Center") ||
                    NameContains(tab, "Operations Command"))
                {
                    if (ClickElement(tab))
                    {
                        Thread.Sleep(400);
                        return true;
                    }
                }
            }
            catch
            {
                // Stale UIA nodes during TabView hydration.
            }
        }

        return ClickByNameContains(window, "Operations Command Center");
    }

    public static bool WaitForDashboardOccReady(AutomationElement window, TimeSpan timeout)
    {
        if (WaitForMarkerOrAutomationId(window, string.Empty,
                "OccSnapshotReady",
                timeout))
        {
            return true;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindMarkerOrAutomationId(window, "DATE RANGE", null) ||
                FindMarkerOrAutomationId(window, "From date", null) ||
                FindMarkerOrAutomationId(window, "Refresh", null) ||
                FindMarkerOrAutomationId(window, "Kanban column: New inquiries", null) ||
                FindByNameContains(window, "No professional accounts") is not null)
            {
                return true;
            }

            Thread.Sleep(150);
        }

        return false;
    }

    public static void ScrollDashboardOccIntoView(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        for (var step = 0; step < 4; step++)
        {
            Keyboard.Press(VirtualKeyShort.PRIOR);
            Keyboard.Release(VirtualKeyShort.PRIOR);
            Thread.Sleep(80);
        }

        for (var step = 0; step < 6; step++)
        {
            Keyboard.Press(VirtualKeyShort.NEXT);
            Keyboard.Release(VirtualKeyShort.NEXT);
            Thread.Sleep(120);
        }
    }

    public static string? SafeName(AutomationElement element)
    {
        try
        {
            return element.Name;
        }
        catch
        {
            return null;
        }
    }

    public static bool EnsurePersonalOverviewTab(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        if (ClickByNameOrAutomationId(window, "Personal Overview Tab", "DashboardPersonalTab"))
        {
            Thread.Sleep(400);
            return true;
        }

        foreach (var tab in window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.TabItem)))
        {
            if (NameContains(tab, "Personal Overview") && ClickElement(tab))
            {
                Thread.Sleep(400);
                return true;
            }
        }

        return ClickByNameContains(window, "Personal Overview");
    }

    private static bool ClickElement(AutomationElement target)
    {
        try
        {
            target.Focus();
            if (target.Patterns.Invoke.IsSupported)
            {
                target.Patterns.Invoke.Pattern.Invoke();
                return true;
            }

            if (target.Patterns.ExpandCollapse.IsSupported)
            {
                var pattern = target.Patterns.ExpandCollapse.Pattern;
                if (pattern.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    pattern.Expand();
                }

                return true;
            }

            target.Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool NameContains(AutomationElement element, string fragment)
    {
        try
        {
            var name = element.Name;
            return !string.IsNullOrWhiteSpace(name) &&
                   name.Contains(fragment, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
