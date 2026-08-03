namespace UnifiedMessenger.Models;

/// <summary>
/// A top-level destination in the shell's left navigation — one page hosted in the shell's
/// <c>ContentFrame</c>.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the "three booleans plus a nullable string" encoding that previously described which
/// destination was active, duplicated across the navigation coordinator, the window view-model, the
/// selection state record and the sidebar helper. Adding destinations that way was combinatorial; adding
/// one here is a single enum member.
/// </para>
/// <para>
/// An account WebView is deliberately <b>not</b> a section. Selecting an account collapses the
/// <c>ContentFrame</c> and shows the per-account WebView host instead — a different axis, tracked
/// separately as the selected instance id. See <c>ShellNavigationCoordinator</c>.
/// </para>
/// </remarks>
public enum ShellSection
{
    Dashboard,

    Analytics,

    Reviews,

    Reports,

    Settings
}
