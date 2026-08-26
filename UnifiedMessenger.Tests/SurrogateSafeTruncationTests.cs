using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression cover for the defect that silently dropped a customer from every scan.
/// </summary>
/// <remarks>
/// Measured live on the owner's machine at v4.99.45: <c>[WRN] [ChatEntryParser] Skipped 1 of 894
/// conversation rows as unparseable: InvalidOperationException: Cannot read incomplete UTF-16 JSON text
/// as string with missing low surrogate.</c> — on every pass, the same row. The cause was preview
/// truncation slicing by UTF-16 code unit: an emoji is a surrogate pair, so a cut at the boundary emits a
/// lone high surrogate, <c>JSON.stringify</c> writes a bare <c>\uD83D</c>, and System.Text.Json throws
/// when it reads that property. The row was caught per-row and discarded, so a real conversation was
/// invisible to the dashboard while the log said everything was fine.
/// </remarks>
public class SurrogateSafeTruncationTests
{
    // U+1F600 GRINNING FACE — a surrogate pair, so each one is 2 UTF-16 units.
    private const string Emoji = "\U0001F600";

    private static string ReadScript(string fileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        Assert.True(File.Exists(scriptPath), $"Missing script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }

    [Fact]
    public void Truncate_DoesNotOrphanAHighSurrogate()
    {
        // 7 chars then an emoji: cutting at 8 would land between the two halves of the pair.
        var text = "abcdefg" + Emoji;

        var result = TranscriptBuilder.Truncate(text, 8);

        Assert.Equal("abcdefg", result);
        Assert.DoesNotContain(result, c => char.IsSurrogate(c));
    }

    [Fact]
    public void Truncate_KeepsAWholeSurrogatePairThatFits()
    {
        var text = "abcdefg" + Emoji + "hij";

        var result = TranscriptBuilder.Truncate(text, 9);

        Assert.Equal("abcdefg" + Emoji, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Truncate_ReturnsInputWhenLimitIsNotPositive(int max) =>
        Assert.Equal("abc", TranscriptBuilder.Truncate("abc", max));

    [Fact]
    public void Truncate_LeavesShortTextAlone() =>
        Assert.Equal("abc", TranscriptBuilder.Truncate("abc", 800));

    [Fact]
    public void Build_BoundsTheMessageAndNeverEmitsALoneSurrogate()
    {
        // An 800-unit run of emoji puts a surrogate pair exactly on the 800-char boundary.
        var item = new MessageTriageItem
        {
            Id = "t1",
            InstanceId = "wa-1",
            InstanceDisplayName = "Gulberg WhatsApp",
            CustomerName = "Ayesha",
            Platform = "whatsapp",
            MessagePreview = string.Empty,
            MessageFullText = string.Concat(Enumerable.Repeat(Emoji, 500))
        };

        var prompt = TranscriptBuilder.Build(item);

        for (var i = 0; i < prompt.Length; i++)
        {
            if (char.IsHighSurrogate(prompt[i]))
            {
                Assert.True(
                    i + 1 < prompt.Length && char.IsLowSurrogate(prompt[i + 1]),
                    $"Orphaned high surrogate at index {i}.");
                i++; // consume the low half of a well-formed pair
                continue;
            }

            Assert.False(char.IsLowSurrogate(prompt[i]), $"Orphaned low surrogate at index {i}.");
        }
    }

    [Fact]
    public void AdapterCoreScript_DefinesTheSharedSurrogateSafeTruncator()
    {
        var script = ReadScript("adapter-core.js");

        Assert.Contains("window.__umTruncate", script, StringComparison.Ordinal);

        // The guard itself: the high-surrogate range. If this range check is ever removed the helper
        // silently degrades back to a plain slice, which is exactly the bug this file exists for.
        Assert.Contains("0xd800", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0xdbff", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("adapter-core.js", "messagePreview.slice(0, 48)")]
    [InlineData("adapter-core.js", "text.slice(0, 157)")]
    [InlineData("whatsapp-adapter.js", "preview.slice(0, 90)")]
    [InlineData("whatsapp-adapter.js", "preview.slice(0, 100)")]
    [InlineData("whatsapp-store-bridge.js", "bodyOf(last).slice(0, 120)")]
    public void PreviewProducers_NoLongerCutByRawCodeUnit(string fileName, string oldSlice) =>
        Assert.DoesNotContain(oldSlice, ReadScript(fileName), StringComparison.Ordinal);

    [Theory]
    [InlineData("adapter-core.js")]
    [InlineData("whatsapp-adapter.js")]
    [InlineData("whatsapp-store-bridge.js")]
    public void PreviewProducers_RouteThroughTheSharedTruncator(string fileName) =>
        Assert.Contains("__umTruncate", ReadScript(fileName), StringComparison.Ordinal);
}
