using UnifiedMessenger.Services;
using Windows.System;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The three features built to attack the measured daily cost: a 61-item queue, four questions asked over
/// and over, and 81 customers who called and got nothing back.
/// </summary>
public class TriageWorkflowTests
{
    // ---- Keyboard map -------------------------------------------------------------------------------

    [Theory]
    [InlineData(VirtualKey.J, TriageCommand.Next)]
    [InlineData(VirtualKey.Down, TriageCommand.Next)]
    [InlineData(VirtualKey.K, TriageCommand.Previous)]
    [InlineData(VirtualKey.Up, TriageCommand.Previous)]
    [InlineData(VirtualKey.Enter, TriageCommand.Open)]
    [InlineData(VirtualKey.O, TriageCommand.Open)]
    [InlineData(VirtualKey.D, TriageCommand.MarkDone)]
    [InlineData(VirtualKey.S, TriageCommand.Snooze)]
    [InlineData(VirtualKey.C, TriageCommand.CallBack)]
    [InlineData(VirtualKey.R, TriageCommand.CopyReply)]
    [InlineData(VirtualKey.Home, TriageCommand.First)]
    [InlineData(VirtualKey.End, TriageCommand.Last)]
    public void TheKeysDoWhatADecadeOfMailClientsTaught(VirtualKey key, TriageCommand expected)
    {
        Assert.Equal(expected, TriageKeyboard.Resolve(key, anyModifierHeld: false, typingInAField: false));
    }

    [Theory]
    [InlineData(VirtualKey.D)]
    [InlineData(VirtualKey.S)]
    [InlineData(VirtualKey.J)]
    [InlineData(VirtualKey.R)]
    public void AModifiedKeypressIsNeverATriageCommand(VirtualKey key)
    {
        // Ctrl+D and Ctrl+F belong to the shell and the browser. A single-letter shortcut that also fires
        // with a modifier held is how an application command silently eats an accelerator.
        Assert.Equal(
            TriageCommand.None,
            TriageKeyboard.Resolve(key, anyModifierHeld: true, typingInAField: false));
    }

    [Theory]
    [InlineData(VirtualKey.D)]
    [InlineData(VirtualKey.J)]
    [InlineData(VirtualKey.Enter)]
    public void NothingFiresWhileTheOwnerIsTyping(VirtualKey key)
    {
        // The queue's search box sits directly above the list. Without this, typing "done" would mark three
        // conversations handled and search for "e".
        Assert.Equal(
            TriageCommand.None,
            TriageKeyboard.Resolve(key, anyModifierHeld: false, typingInAField: true));
    }

    // ---- Selection movement -------------------------------------------------------------------------

    [Fact]
    public void MovingDownFromNothingSelectedLandsOnTheFirstRow()
    {
        Assert.Equal(0, TriageKeyboard.Move(TriageCommand.Next, currentIndex: -1, count: 10));
    }

    [Fact]
    public void TheSelectionClampsRatherThanWrapping()
    {
        // Wrapping is disorienting on a long backlog: one extra J jumps to the top and the owner loses their
        // place in a list they were working through in order.
        Assert.Equal(9, TriageKeyboard.Move(TriageCommand.Next, currentIndex: 9, count: 10));
        Assert.Equal(0, TriageKeyboard.Move(TriageCommand.Previous, currentIndex: 0, count: 10));
    }

    [Fact]
    public void AnEmptyQueueHasNothingToSelect()
    {
        foreach (var command in new[] { TriageCommand.Next, TriageCommand.First, TriageCommand.Last })
        {
            Assert.Equal(-1, TriageKeyboard.Move(command, currentIndex: 0, count: 0));
        }
    }

    [Fact]
    public void AStaleIndexIsBroughtBackIntoRange()
    {
        // The list re-renders every 20 seconds and rows leave it as they are handled. An index that outlived
        // its row must not throw or select nothing.
        Assert.Equal(2, TriageKeyboard.Move(TriageCommand.Next, currentIndex: 99, count: 3));
    }

    [Fact]
    public void ClearingARowKeepsYouWhereYouWere()
    {
        // Mark done, and the selection should land on whatever took that row's place — not jump to the top.
        Assert.Equal(4, TriageKeyboard.IndexAfterRemoval(removedIndex: 4, newCount: 10));

        // Unless it was the last row, in which case it steps back rather than off the end.
        Assert.Equal(8, TriageKeyboard.IndexAfterRemoval(removedIndex: 9, newCount: 9));

        // And clearing the only row leaves nothing selected.
        Assert.Equal(-1, TriageKeyboard.IndexAfterRemoval(removedIndex: 0, newCount: 0));
    }

    [Fact]
    public void EveryCommandIsDocumentedForTheHelpOverlay()
    {
        Assert.NotEmpty(TriageKeyboard.Shortcuts);
        Assert.All(TriageKeyboard.Shortcuts, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Keys));
            Assert.False(string.IsNullOrWhiteSpace(s.Does));
        });
    }

    // ---- Facets -------------------------------------------------------------------------------------

    [Fact]
    public void AMissedCallIsItsOwnKindOfRowNotAnUnreadableMessage()
    {
        // 81 of these were sitting in the queue as unreadable rows because a call log carries no text.
        // They need a call back, not a message, which is a different action — so a different facet.
        Assert.Equal(
            QueueFacet.MissedCall,
            QueueFacets.Resolve(ReplyNeedReason.MissedCall, preview: ""));
        Assert.True(QueueFacets.IsCallBack(QueueFacet.MissedCall));
    }

    [Theory]
    [InlineData(ReplyNeedReason.MediaWithoutCaption, QueueFacet.Media)]
    [InlineData(ReplyNeedReason.NoPreviewAvailable, QueueFacet.Unreadable)]
    public void HowTheRowArrivedBeatsWhatItsTextSays(ReplyNeedReason reason, QueueFacet expected)
    {
        // A photo and an unreadable message are different KINDS of row. No amount of topic classification on
        // whatever text happens to be there changes what the owner can do about them.
        Assert.Equal(expected, QueueFacets.Resolve(reason, preview: "kitna charge hoga"));
    }

    [Theory]
    [InlineData("V v v unprofessional staff my girls got bruises on legs", QueueFacet.AtRisk)]
    [InlineData("kitna charge hoga", QueueFacet.Enquiry)]
    [InlineData("I want to reschedule my appointment", QueueFacet.Booking)]
    [InlineData("I need jop dear", QueueFacet.JobApplicant)]
    [InlineData("Mel to mel", QueueFacet.Unknown)]
    public void OrdinaryTextStillFallsThroughToTheTopic(string preview, QueueFacet expected)
    {
        Assert.Equal(expected, QueueFacets.Resolve(ReplyNeedReason.Substantive, preview));
    }

    [Fact]
    public void TheChipsAreOrderedByWhatCostsMoney()
    {
        // Not alphabetical and not the enum order: at-risk first, then the calls, then the earners, then
        // what can be set aside.
        var order = QueueFacets.DisplayOrder;

        Assert.Equal(QueueFacet.AtRisk, order[0]);
        Assert.Equal(QueueFacet.MissedCall, order[1]);
        Assert.True(
            Array.IndexOf(order, QueueFacet.Enquiry) < Array.IndexOf(order, QueueFacet.JobApplicant),
            "Enquiries must be offered before job applicants.");
        Assert.Equal(QueueFacet.Unknown, order[^1]);
    }

    [Fact]
    public void EveryFacetHasALabelAndAnExplanation()
    {
        foreach (QueueFacet facet in Enum.GetValues<QueueFacet>())
        {
            Assert.False(string.IsNullOrWhiteSpace(QueueFacets.Label(facet)));
            Assert.False(string.IsNullOrWhiteSpace(QueueFacets.Describe(facet)));
        }
    }

    // ---- Saved replies ------------------------------------------------------------------------------

    [Fact]
    public void APlaceholderIsFilledFromTheConversation()
    {
        var filled = SavedReplyText.Fill(
            "Hi {first}, thanks for contacting {branch}.",
            customerName: "Ambareen Rizvi",
            branch: "F-11 Islamabad",
            account: "Depilex F-11 WhatsApp");

        Assert.Equal("Hi Ambareen, thanks for contacting F-11 Islamabad.", filled);
    }

    [Fact]
    public void AnUnsavedContactGetsAGreetingRatherThanTheirPhoneNumber()
    {
        // WhatsApp gives a number instead of a name for an unsaved contact, and "Hi +923105325598" reads
        // worse than no name at all. 42% of this owner's waiting chats are unsaved contacts.
        var filled = SavedReplyText.Fill(
            "Hi {first}, how can we help?", "+92 310 5325598", "DHA-2", "Depilex DHA-2 WhatsApp");

        Assert.Equal("Hi there, how can we help?", filled);
    }

    [Theory]
    [InlineData("+923105325598", true)]
    [InlineData("92 310 532 5598", true)]
    [InlineData("(0310) 5325598", true)]
    [InlineData("Ambareen Rizvi", false)]
    [InlineData("Ali", false)]
    [InlineData("Room 4", false)]
    public void ANameIsOnlyTreatedAsANumberWhenItReallyIsOne(string name, bool expected)
    {
        Assert.Equal(expected, SavedReplyText.LooksLikeAPhoneNumber(name));
    }

    [Fact]
    public void AnUnknownPlaceholderIsLeftVisibleRatherThanBlanked()
    {
        // A reply that silently loses a word is worse than one that visibly still has {whatever} in it. The
        // owner reads the text before sending either way, and only one of those two mistakes is catchable.
        var filled = SavedReplyText.Fill("Hi {first}, your {service} is at {time}.", "Ali", "DHA-2", "acct");

        Assert.Contains("{service}", filled, StringComparison.Ordinal);
        Assert.Contains("{time}", filled, StringComparison.Ordinal);
        Assert.StartsWith("Hi Ali,", filled, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingConversationDetailsNeverProduceTheWordNull()
    {
        var filled = SavedReplyText.Fill("Hi {first} at {branch}.", null, null, null);

        Assert.DoesNotContain("null", filled, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("there", filled, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultLibraryAnswersTheQuestionsTheDataSaysGetAsked()
    {
        // Enquiries measured on real traffic are dominated by four questions: what do you charge, what
        // services, what timings, where are you. An empty library is a feature the owner has to build
        // before it does anything.
        var defaults = SavedReplyStore.BuildDefaults();

        Assert.Contains(defaults, r => r.Facets.Contains(QueueFacet.Enquiry));
        Assert.Contains(defaults, r => r.Facets.Contains(QueueFacet.AtRisk));
        Assert.Contains(defaults, r => r.Facets.Contains(QueueFacet.MissedCall));
        Assert.Contains(defaults, r => r.Facets.Contains(QueueFacet.Booking));

        // At least one always-offered reply, or a row with an unrecognised facet gets an empty menu.
        Assert.Contains(defaults, r => r.Facets.Count == 0);
        Assert.All(defaults, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.False(string.IsNullOrWhiteSpace(r.Body));
        });
    }

    [Fact]
    public void ARowIsOfferedItsOwnRepliesFirstAndThenTheGeneralOnes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"um-replies-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SavedReplyStore(path);
            store.LoadAsync().GetAwaiter().GetResult();

            var forEnquiry = store.ForFacet(QueueFacet.Enquiry);

            Assert.NotEmpty(forEnquiry);
            Assert.Contains(QueueFacet.Enquiry, forEnquiry[0].Facets);

            // The general "holding reply" is still available, just after the specific ones.
            Assert.Contains(forEnquiry, r => r.Facets.Count == 0);

            // A reply written for complaints must not be suggested on a pricing question.
            Assert.DoesNotContain(forEnquiry, r => r.Facets.Contains(QueueFacet.AtRisk));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void TheLibrarySurvivesARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"um-replies-{Guid.NewGuid():N}.json");
        try
        {
            var first = new SavedReplyStore(path);
            first.LoadAsync().GetAwaiter().GetResult();
            first.UpsertAsync(new SavedReply
            {
                Title = "Eid timings",
                Body = "We are closed on [dates] for Eid.",
                Facets = [QueueFacet.Enquiry]
            }).GetAwaiter().GetResult();

            var second = new SavedReplyStore(path);
            second.LoadAsync().GetAwaiter().GetResult();

            Assert.Contains(second.All, r => r.Title == "Eid timings");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ACorruptLibraryFallsBackToTheDefaultsInsteadOfEmptying()
    {
        // The settings file has already corrupted once on this install. Losing the owner's whole set of
        // replies silently would be the same failure with a different file.
        var path = Path.Combine(Path.GetTempPath(), $"um-replies-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");

            var store = new SavedReplyStore(path);
            store.LoadAsync().GetAwaiter().GetResult();

            Assert.NotEmpty(store.All);
            Assert.True(File.Exists(path + ".corrupt"), "The unreadable file should be kept, not discarded.");
        }
        finally
        {
            foreach (var p in new[] { path, path + ".corrupt" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }
    }
}
