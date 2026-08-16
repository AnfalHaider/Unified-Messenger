using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-DURA-03 — behaviour of the durable stores against files an interrupted write can actually leave.
///
/// The danger being tested for: a file that <em>parses successfully</em> but has lost records. Corruption
/// detection only fires on a parse failure, so a file that deserializes cleanly to nothing would silently
/// reset the store, produce no log line and no <c>.bak</c> — defeating the recovery added in v4.99.4/.5
/// entirely. A zero-byte file is the most likely real-world outcome of an interrupted write, and a
/// literal <c>null</c> is the classic "valid JSON, no data" case.
/// </summary>
public class TornWriteRecoveryTests
{
    private sealed class Box
    {
        public Dictionary<string, string> Items { get; set; } = [];
    }

    private static async Task<(bool Threw, Box? Result)> TryDeserializeAsync(string content)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        try
        {
            var box = await JsonSerializer.DeserializeAsync<Box>(stream);
            return (false, box);
        }
        catch (JsonException)
        {
            return (true, null);
        }
    }

    [Fact]
    public async Task ZeroByteFileThrows_SoCorruptionRecoveryFires()
    {
        // The critical case. If this returned null instead of throwing, every store's
        // "?? new()" / "store?.X ?? []" fallback would reset the user's data silently.
        var (threw, _) = await TryDeserializeAsync(string.Empty);

        Assert.True(threw, "a zero-byte store file must be treated as corrupt, not as empty data");
    }

    [Fact]
    public async Task WhitespaceOnlyFileThrows_SoCorruptionRecoveryFires()
    {
        var (threw, _) = await TryDeserializeAsync("   \r\n  ");

        Assert.True(threw);
    }

    [Fact]
    public async Task TruncatedJsonThrows_SoCorruptionRecoveryFires()
    {
        // What a half-written file looks like.
        var (threw, _) = await TryDeserializeAsync("{ \"items\": { \"a\": \"1\", \"b\": ");

        Assert.True(threw);
    }

    [Fact]
    public async Task LiteralNullDeserializesToNull_WhichEveryStoreMustTreatAsNoData()
    {
        // This one does NOT throw — it is valid JSON. It is therefore the one shape that slips past
        // corruption detection. Documented here so the behaviour is deliberate rather than a surprise:
        // every store's load path must null-check the result, which they all do
        // ("?? new AppSettings()", "store?.Instances ?? []", "store?.Days ?? []").
        var (threw, result) = await TryDeserializeAsync("null");

        Assert.False(threw);
        Assert.Null(result);
    }

    [Fact]
    public async Task WellFormedButEmptyObjectIsIndistinguishableFromRealEmptyData()
    {
        // "{}" is a legitimate saved state (the user has no overrides). It cannot and should not be
        // treated as corruption. This is the residual risk the atomic write is what actually protects
        // against — see the F-DURA-03 finding.
        var (threw, result) = await TryDeserializeAsync("{}");

        Assert.False(threw);
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
    }

    [Fact]
    public void ZeroByteAndTruncatedFilesAreClassifiedUnreadable()
    {
        // Ties the above back to the shared recovery helper: the exception these produce is the one
        // CorruptFileRecovery acts on, so they get logged and preserved rather than silently dropped.
        Assert.True(CorruptFileRecovery.IsUnreadable(new JsonException("no JSON tokens")));
    }
}
