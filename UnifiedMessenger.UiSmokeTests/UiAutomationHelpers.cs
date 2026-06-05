using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

internal static class UiAutomationHelpers
{
    public static bool WaitForMarker(AutomationElement root, string marker, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindByName(root, marker) is not null)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    public static AutomationElement? FindByName(AutomationElement root, string name) =>
        root.FindFirstDescendant(root.ConditionFactory.ByName(name));

    public static bool ClickByName(AutomationElement root, string name)
    {
        var target = FindByName(root, name);
        if (target is null)
        {
            return false;
        }

        try
        {
            target.Focus();
            if (target.Patterns.Invoke.IsSupported)
            {
                target.Patterns.Invoke.Pattern.Invoke();
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

    public static void FocusWindow(AutomationElement window)
    {
        try
        {
            window.Focus();
            window.SetForeground();
        }
        catch
        {
            window.Focus();
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

    public static IReadOnlyList<string> SampleNames(AutomationElement root, int max = 20)
    {
        var condition = root.ConditionFactory.ByControlType(ControlType.Text);
        return root.FindAllDescendants(condition)
            .Select(element => element.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
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
