using UnifiedMessenger.Dialogs;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-DIALOG-01 — the icon picker announced twenty-five identical "button"s.
///
/// <para>
/// Found by opening <c>ChangeIconDialog</c> in the running app and reading the UI Automation tree — the
/// same tree a screen reader consumes. Every icon choice, all fourteen brand logos and nine general
/// icons plus the two import/upload rows, came back with no accessible name at all. The dialog was
/// operable only by sight: a screen-reader user could tab through two dozen indistinguishable controls
/// and never learn which one was WhatsApp and which was a shopping cart. The names existed all along,
/// but only as trailing <c>//</c> comments beside each glyph.
/// </para>
/// <para>
/// <b>What can and cannot be tested here.</b> The names are attached with
/// <c>AutomationProperties.SetName</c> during construction, and constructing a <c>ContentDialog</c>
/// needs a UI thread that a plain xUnit host cannot provide (<c>DispatcherQueue.GetForCurrentThread()</c>
/// throws <c>COMException: ClassFactory cannot supply requested class</c>). So these tests pin the part
/// that is pure and is also the part that would realistically rot: the name arrays are positionally
/// parallel to the glyph arrays. Add an icon without adding a name and the count assertion fails
/// immediately, rather than shipping one more silent "button".
/// </para>
/// </summary>
public class DialogAccessibilityTests
{
    [Fact]
    public void EveryBrandIconHasAName()
    {
        Assert.Equal(ChangeIconDialog.BrandIcons.Length, ChangeIconDialog.BrandIconNames.Length);
    }

    [Fact]
    public void EveryGeneralIconHasAName()
    {
        Assert.Equal(ChangeIconDialog.GeneralIcons.Length, ChangeIconDialog.GeneralIconNames.Length);
    }

    [Fact]
    public void NoIconNameIsBlank()
    {
        // A whitespace name is indistinguishable from no name to a screen reader, and would slip past a
        // count-only check.
        Assert.All(ChangeIconDialog.BrandIconNames, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        Assert.All(ChangeIconDialog.GeneralIconNames, n => Assert.False(string.IsNullOrWhiteSpace(n)));
    }

    [Fact]
    public void IconNamesAreDistinctSoTwoChoicesNeverSoundIdentical()
    {
        var all = ChangeIconDialog.BrandIconNames.Concat(ChangeIconDialog.GeneralIconNames).ToList();

        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheNamesAreTheRecognisableProductNamesNotGlyphCodepoints()
    {
        // Guards against someone "fixing" a future gap by filling the array with placeholders. The
        // fallback in BuildIconWrap deliberately produces "Icon N" only when the arrays drift; that
        // string must never be the intended answer.
        Assert.Contains("WhatsApp", ChangeIconDialog.BrandIconNames);
        Assert.Contains("Instagram", ChangeIconDialog.BrandIconNames);
        Assert.DoesNotContain(ChangeIconDialog.BrandIconNames, n => n.StartsWith("Icon ", StringComparison.Ordinal));
        Assert.DoesNotContain(ChangeIconDialog.GeneralIconNames, n => n.StartsWith("Icon ", StringComparison.Ordinal));

        // Not "short" — "X" is a real product name and a one-character one. What must never appear is a
        // Private Use Area codepoint, i.e. someone pasting the glyph itself in as its own label.
        Assert.All(
            ChangeIconDialog.BrandIconNames.Concat(ChangeIconDialog.GeneralIconNames),
            n => Assert.DoesNotContain(n, c => c >= '' && c <= ''));
    }
}
