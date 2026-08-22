using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Controls;

/// <summary>
/// A sidebar row that assistive technology can actually activate.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this fixes.</b> Every navigable row in the rail — Dashboard, Analytics, Reviews, Reports
/// and every account — was a plain <see cref="Border"/> with pointer and key handlers. A Border exposes no
/// automation pattern, so the row announced itself as a <c>Group</c> and offered nothing to invoke. Found
/// while driving the app through UI Automation: the Reviews row could not be activated at all, and the only
/// way in was to compute its rectangle and click by screen coordinates. Enter and Space did work for someone
/// already focused on it, so the capability existed and only the exposure was missing — but any tool that
/// asks "what can I do with this element?" was told nothing.
/// </para>
/// <para>
/// <b>Why a peer rather than a Button.</b> Rebuilding the rail out of Buttons would inherit a control
/// template with its own padding, focus visuals and pointer states, and the rows carry a selection accent
/// bar and compact/expanded density that the existing layout already gets right. The peer adds the missing
/// contract — control type Button, an <see cref="IInvokeProvider"/> — without touching a pixel.
/// </para>
/// <para>
/// <b>Why ContentControl and not Border.</b> Border is sealed in WinUI 3, so it cannot host a custom peer.
/// ContentControl carries the same Background, BorderBrush, BorderThickness, CornerRadius and Padding the
/// rows already set, so the swap is a property rename (<c>Child</c> to <c>Content</c>) rather than a
/// restyle. Content alignment is forced to Stretch because ContentControl defaults to Left/Top where
/// Border stretched its child.
/// </para>
/// </remarks>
public sealed partial class NavigationRow : ContentControl
{
    /// <summary>
    /// What activating this row does. The same action the row's own Enter/Space handler performs.
    /// </summary>
    /// <remarks>
    /// Deliberately a separate hook rather than a synthesised key press: raising the navigation event
    /// directly is what the keyboard path already does, so both routes end in one place and cannot drift.
    /// </remarks>
    public Action? Invoked { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationRowAutomationPeer(this);
}

internal sealed class NavigationRowAutomationPeer(NavigationRow owner)
    : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    private readonly NavigationRow _owner = owner;

    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

    /// <summary>Reported as a button, because that is what it behaves like.</summary>
    /// <remarks>
    /// A screen reader announcing "Reviews, Overview, press to open" as a <i>group</i> gives the listener no
    /// reason to think pressing anything will help. Announcing it as a button is the difference between a
    /// row that reads as a label and one that reads as a control.
    /// </remarks>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(NavigationRow);

    public void Invoke() =>
        // Automation calls arrive off the UI thread; the navigation this triggers touches WebView2 and the
        // shell, both of which are UI-thread-only.
        _owner.DispatcherQueue.TryEnqueue(() => _owner.Invoked?.Invoke());
}
