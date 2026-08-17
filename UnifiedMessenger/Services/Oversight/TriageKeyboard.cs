using Windows.System;

namespace UnifiedMessenger.Services;

/// <summary>What a keypress means while working the reply queue.</summary>
public enum TriageCommand
{
    None,
    Next,
    Previous,
    First,
    Last,
    Open,
    MarkDone,
    Snooze,
    CallBack,
    CopyReply,
    ShowHelp
}

/// <summary>
/// The keyboard map for working the reply queue.
///
/// <para>
/// <b>Why this is the highest-value thing on the roadmap.</b> Sixty-one conversations at four clicks each —
/// find the row, open it, come back, mark it done — is a morning. The same queue with one hand on the
/// keyboard is ten minutes. Nothing else in the backlog changes the daily job by that margin, and it needs
/// no new data.
/// </para>
/// <para>
/// <b>Why J/K and not only the arrows.</b> Both work. Arrows are what someone tries first; J/K is what
/// someone who does this every morning settles into, and it costs one extra line to support. The letters are
/// the ones a decade of mail and code-review tools have already taught: J down, K up, O open, D done.
/// </para>
/// <para>
/// <b>Kept pure.</b> Resolution is a static function of key plus modifiers, so the whole map is asserted by
/// test without a window — including the rule that matters most, which is that a modified keypress is never
/// a triage command.
/// </para>
/// </summary>
public static class TriageKeyboard
{
    /// <summary>
    /// Maps a keypress to a command.
    /// </summary>
    /// <param name="anyModifierHeld">
    /// True when Ctrl, Alt or Windows is down. Such a press must never resolve to a command: <c>Ctrl+D</c>
    /// and <c>Ctrl+F</c> belong to the shell and the browser, and a single-letter shortcut that also fires
    /// with a modifier held is how an accelerator silently eats an application command.
    /// </param>
    /// <param name="typingInAField">
    /// True when focus is in a text box. The queue's own search box sits directly above the list, so a map
    /// that fired while typing would turn the word "done" into three commands and a search for "e".
    /// </param>
    public static TriageCommand Resolve(VirtualKey key, bool anyModifierHeld, bool typingInAField)
    {
        if (anyModifierHeld || typingInAField)
        {
            return TriageCommand.None;
        }

        return key switch
        {
            VirtualKey.J or VirtualKey.Down => TriageCommand.Next,
            VirtualKey.K or VirtualKey.Up => TriageCommand.Previous,
            VirtualKey.Home => TriageCommand.First,
            VirtualKey.End => TriageCommand.Last,

            // Enter opens because that is what Enter does everywhere; O is the muscle-memory alias.
            VirtualKey.Enter or VirtualKey.O => TriageCommand.Open,

            VirtualKey.D => TriageCommand.MarkDone,
            VirtualKey.S => TriageCommand.Snooze,
            VirtualKey.C => TriageCommand.CallBack,
            VirtualKey.R => TriageCommand.CopyReply,

            // "?" arrives as Shift+/ on most layouts, and Shift is not one of the modifiers that blocks a
            // command — otherwise the help key could never be pressed.
            VirtualKey.Divide => TriageCommand.ShowHelp,
            (VirtualKey)191 => TriageCommand.ShowHelp, // VK_OEM_2, the '/?' key
            _ => TriageCommand.None
        };
    }

    /// <summary>
    /// Moves the selection. Returns the new index, clamped, or -1 when there is nothing to select.
    /// </summary>
    /// <remarks>
    /// Clamps rather than wraps. Wrapping from the last row back to the first is disorienting when the list
    /// is long: the owner presses J once more, the view jumps to the top, and they lose their place in a
    /// backlog they were working through in order.
    /// </remarks>
    public static int Move(TriageCommand command, int currentIndex, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        var index = currentIndex < 0 ? -1 : Math.Min(currentIndex, count - 1);

        return command switch
        {
            // From no selection, Next lands on the first row rather than the second.
            TriageCommand.Next => index < 0 ? 0 : Math.Min(index + 1, count - 1),
            TriageCommand.Previous => index <= 0 ? 0 : index - 1,
            TriageCommand.First => 0,
            TriageCommand.Last => count - 1,
            _ => index
        };
    }

    /// <summary>
    /// After an action removes the selected row, the selection should land on what took its place — not jump
    /// to the top, and not fall off the end when the last row was the one cleared.
    /// </summary>
    public static int IndexAfterRemoval(int removedIndex, int newCount) =>
        newCount <= 0 ? -1 : Math.Clamp(removedIndex, 0, newCount - 1);

    /// <summary>The shortcut list, for the help overlay and the accessible description.</summary>
    public static readonly (string Keys, string Does)[] Shortcuts =
    [
        ("J / ↓", "next conversation"),
        ("K / ↑", "previous conversation"),
        ("Home / End", "first or last"),
        ("Enter / O", "open the conversation"),
        ("D", "mark as done"),
        ("S", "snooze"),
        ("C", "call back"),
        ("R", "copy a saved reply"),
        ("?", "show this list")
    ];
}
