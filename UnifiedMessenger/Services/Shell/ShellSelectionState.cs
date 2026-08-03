using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Shell;

/// <summary>
/// What the shell is currently showing: a section page, or an account's WebView when
/// <see cref="SelectedInstanceId"/> is set (the two are different axes, not alternatives in one enum).
/// </summary>
public readonly record struct ShellSelectionState(
    ShellSection Section,
    string? SelectedInstanceId);
