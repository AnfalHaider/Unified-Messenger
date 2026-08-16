using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The local model only ever rules on what the word rules could not settle, and it can only ever move a
/// conversation from counted to closed.
///
/// <para>
/// The failure worth guarding against is not a wrong answer — it is the model being <i>absent</i> or
/// <i>unintelligible</i> and the queue silently shrinking anyway. Most of these tests are about what
/// happens when nothing useful comes back.
/// </para>
/// </summary>
public class ReplyNeedAdjudicatorTests
{
    private static ReplyNeedAdjudicator Build(
        string? answer,
        bool available = true,
        Action<string>? onPrompt = null,
        int[]? callCount = null) =>
        new(
            (prompt, _, _, _) =>
            {
                onPrompt?.Invoke(prompt);
                if (callCount is not null)
                {
                    callCount[0]++;
                }

                return Task.FromResult(answer);
            },
            () => available,
            () => "phi3:mini");

    [Fact]
    public async Task OnlyTheMessagesTheRulesCouldNotSettleAreEverSentToTheModel()
    {
        var prompts = new List<string>();
        var adjudicator = Build("DONE", onPrompt: prompts.Add);

        await adjudicator.RequestAsync(
        [
            "ok",                                // rules: acknowledgement
            "kitna charge hoga",                 // rules: asks something
            "",                                  // rules: unreadable
            "Photo",                             // rules: media
            "Both signature and senior artist"   // rules: no idea — this is the one
        ]);

        var prompt = Assert.Single(prompts);
        Assert.Contains("Both signature and senior artist", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameMessageIsOnlyEverAskedAboutOnce()
    {
        // "ok" typed by forty customers is one inference, not forty. The cache is keyed on the message,
        // so this also holds across accounts and across refreshes.
        var calls = new[] { 0 };
        var adjudicator = Build("DONE", callCount: calls);

        await adjudicator.RequestAsync(["Mel to mel", "Mel to mel", "Mel to mel"]);
        await adjudicator.RequestAsync(["Mel to mel"]);

        Assert.Equal(1, calls[0]);
    }

    [Fact]
    public async Task AModelThatIsTurnedOffChangesNothing()
    {
        var calls = new[] { 0 };
        var adjudicator = Build("DONE", available: false, callCount: calls);

        var decided = await adjudicator.RequestAsync(["Both signature and senior artist"]);

        Assert.Equal(0, decided);
        Assert.Equal(0, calls[0]);
        Assert.Null(adjudicator.TryGetNeedsReply("Both signature and senior artist"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I think the customer is probably finished here")]
    [InlineData("MAYBE")]
    [InlineData("REPLY or DONE depending on context")]
    public async Task AnUnusableAnswerLeavesTheConversationCounted(string? answer)
    {
        // A hedging or rambling model must not be able to clear someone's queue. The assertion is on the
        // outcome, not on the cache: "REPLY or DONE depending on context" is read as REPLY, which is a
        // usable answer meaning keep it. What must never happen is the chat coming back closed.
        var adjudicator = Build(answer);

        await adjudicator.RequestAsync(["Both signature and senior artist"]);

        Assert.NotEqual(false, adjudicator.TryGetNeedsReply("Both signature and senior artist"));
    }

    [Theory]
    [InlineData("DONE", false)]
    [InlineData("done", false)]
    [InlineData("DONE.", false)]
    [InlineData("DONE - the customer is just saying thanks", false)]
    [InlineData("REPLY", true)]
    [InlineData("reply", true)]
    [InlineData("REPLY, they asked about pricing", true)]
    public void OnlyTheFirstWordOfTheAnswerIsRead(string answer, bool expected)
    {
        // Small models routinely explain themselves. The reasoning after the verdict must not be scanned
        // for the other keyword — "DONE - they didn't ask for a reply" contains both.
        Assert.Equal(expected, ReplyNeedAdjudicator.ParseAnswer(answer));
    }

    [Fact]
    public async Task AClosedVerdictReachesTheCountThroughTheSamePredicateAsEverythingElse()
    {
        var adjudicator = Build("DONE");
        await adjudicator.RequestAsync(["Both signature and senior artist"]);

        Assert.False(adjudicator.TryGetNeedsReply("Both signature and senior artist"));
    }

    [Fact]
    public void OnlyASubstantiveVerdictIsConsideredAmbiguous()
    {
        // The model is not asked to second-guess a question, a closer, or an unreadable chat. Widening
        // this would let it overturn the deterministic rules, which is exactly what it must not do.
        Assert.True(ReplyNeedAdjudicator.IsAmbiguous("Both signature and senior artist"));

        Assert.False(ReplyNeedAdjudicator.IsAmbiguous("kitna charge hoga"));
        Assert.False(ReplyNeedAdjudicator.IsAmbiguous("ok thanks"));
        Assert.False(ReplyNeedAdjudicator.IsAmbiguous(""));
        Assert.False(ReplyNeedAdjudicator.IsAmbiguous("Photo"));
    }

    [Fact]
    public void OnlyABoundedSliceOfAMessageIsEverSent()
    {
        // Ollama is localhost so this is on-box either way, but there is no reason to hand a model more
        // of a customer's words than it needs to answer one yes/no question.
        var long_ = new string('x', ReplyNeedAdjudicator.MaxPromptCharacters * 3);

        Assert.Equal(ReplyNeedAdjudicator.MaxPromptCharacters, ReplyNeedAdjudicator.Normalize(long_)!.Length);
        Assert.Null(ReplyNeedAdjudicator.Normalize("   "));
    }
}
