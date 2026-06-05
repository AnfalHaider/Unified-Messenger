using System.Text.RegularExpressions;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static partial class BranchNameResolver
{
    [GeneratedRegex(
        @"\b(DHA-?\s*\d+|Men\s+DHA-?\s*\d+|F-?\s*\d+|IgnitePro|Eli-?\s*\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BranchTokenPattern();

    public static string Resolve(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Resolve(instance.DisplayName);
    }

    public static string Resolve(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "General";
        }

        var match = BranchTokenPattern().Match(displayName);
        if (!match.Success)
        {
            return displayName.Trim();
        }

        return Regex.Replace(match.Value.Trim(), @"\s+", "-", RegexOptions.CultureInvariant);
    }
}
