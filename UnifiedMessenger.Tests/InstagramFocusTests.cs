namespace UnifiedMessenger.Tests;

/// <summary>
/// Instagram's "take me to the conversation without opening it" path (Increment 134).
///
/// <para>
/// Opening an Instagram thread marks it read and fires a "Seen" to the customer, which cannot be
/// withdrawn and destroys the very signal this app measures. So clicking a row in the needs-a-reply
/// queue filters the Direct list to that conversation and <b>stops</b>. Every assertion here exists to
/// keep that property from eroding.
/// </para>
/// </summary>
public class InstagramFocusTests
{
    private static string Script() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "instagram-adapter.js"));

    [Fact]
    public void TheFocusPathNeverClicksAnything()
    {
        var script = Script();

        // A click is the one operation that turns this from a read into a message the customer can see.
        Assert.DoesNotContain(".click(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("dispatchEvent(new MouseEvent", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PointerEvent", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ItNeverNavigatesIntoAThread()
    {
        var script = Script();

        // /direct/t/<id> is a conversation. It appears in this file only as something to detect and back
        // out of, never as a destination.
        Assert.DoesNotContain("assign('https://www.instagram.com/direct/t/", script, StringComparison.Ordinal);
        Assert.Contains("assign('https://www.instagram.com/direct/inbox/')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ItBacksOutIfItFindsItselfInsideAConversation()
    {
        var script = Script();

        // Leaving the owner in a thread they did not choose is the outcome this path exists to avoid, so
        // landing in one is treated as a failure to recover from rather than as arrival.
        Assert.Contains("insideThread()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ItTypesOnlyIntoASearchFieldAndNeverIntoAComposer()
    {
        var script = Script();

        // The banned interaction is synthesising input into a message composer or clicking send. A search
        // field sends nothing and is invisible to the customer — the two must stay distinguishable in this
        // file, so nothing here may reference a composer at all.
        // A composer on Meta's clients is a contenteditable, never an <input>. This file must not know
        // how to find one.
        Assert.DoesNotContain("contenteditable", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role=\"textbox\"", script, StringComparison.OrdinalIgnoreCase);

        // The send-shaped operations, named precisely rather than by the substring "send" — which occurs
        // in this file's own prose explaining that it sends nothing, and a test that fails on its own
        // documentation teaches people to delete the documentation.
        Assert.DoesNotContain("sendMessage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".submit(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyboardEvent", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'Enter'", script, StringComparison.Ordinal);

        Assert.Contains("SEARCH_INPUTS", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInputIsSetThroughTheNativeSetterSoReactActuallyFilters()
    {
        var script = Script();

        // Assigning .value directly updates the DOM and leaves React's state stale, so the list would not
        // filter and the owner would be dropped on an unfiltered inbox with nothing found.
        Assert.Contains("getOwnPropertyDescriptor", script, StringComparison.Ordinal);
        Assert.Contains("new Event('input', { bubbles: true })", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadbackProvesNoConversationIsOpen()
    {
        var script = Script();

        // The opposite of WhatsApp's readback, which proves one IS open. Running WhatsApp's against
        // Instagram would look for an open conversation, never find one, and report every successful
        // focus as a failure.
        Assert.Contains("__umIgFocusReadback", script, StringComparison.Ordinal);

        var start = script.IndexOf("__umIgFocusReadback", StringComparison.Ordinal);
        var body = script[start..];
        Assert.Contains("insideThread() || !onInbox()", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSearchQueryStripsEmojiButKeepsNonLatinScripts()
    {
        var script = Script();

        // Instagram display names routinely carry emoji — "MahnoorKhan🦋" is real, from the owner's own
        // inbox. Typed verbatim, Instagram's search finds nothing. Stripping them is what makes the filter
        // work, and keeping letters in every script matters because these customers are named in three.
        Assert.Contains("searchableName", script, StringComparison.Ordinal);
        Assert.Contains(@"\p{L}", script, StringComparison.Ordinal);
        Assert.Contains("0xd800", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadbackDoesNotDemandAMatchingRow()
    {
        // Scoped to the readback FUNCTION, not "everything after its name". The loose version broke the
        // moment the preview harvest was added below it — the harvest reads innerText legitimately, and a
        // test that fails because unrelated code appeared later in the file is measuring position, not
        // behaviour.
        var script = Script();
        var start = script.IndexOf("window.__umIgFocusReadback", StringComparison.Ordinal);
        Assert.True(start >= 0, "The readback must exist.");

        var end = script.IndexOf("window.__um", start + 10, StringComparison.Ordinal);
        var body = end > start ? script[start..end] : script[start..];

        // Whether Instagram finds that customer is Instagram's answer, not ours: a name may be
        // unsearchable, or the thread may have moved to Requests. The first version demanded a hit in the
        // page text and failed sixteen times on an emoji name, reporting failure for a navigation that had
        // in fact worked and leaving the owner on a page it had just loaded.
        Assert.DoesNotContain("innerText", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FocusWithNoCustomerNameReportsFailureRatherThanGuessing()
    {
        var script = Script();

        // With nothing to search for there is no filtering to claim. Reporting success would tell the
        // caller the conversation was located when the owner is looking at an unfiltered list.
        var start = script.IndexOf("window.__umFocusConversation", StringComparison.Ordinal);
        var body = script[start..(start + 900)];
        Assert.Contains("if (!query)", body, StringComparison.Ordinal);
    }
}
