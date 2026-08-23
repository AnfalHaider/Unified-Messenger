using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Who gets asked for a Google review, and the promise that nobody is asked twice.
/// </summary>
/// <remarks>
/// Every candidate this produces is a real message to a real phone number. The cost of a bad pick is not a
/// wrong figure on a dashboard — it is a customer being pestered. So the rules only ever remove people, and
/// these tests exist mostly to prove the removals work.
/// </remarks>
public class ReviewAskTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"um-asks-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static OversightChatSnapshotService.ChatEntry Chat(
        string preview,
        string phone = "923001234567",
        string key = "923001234567@c.us",
        string name = "Tayyaba Qasim",
        int daysAgo = 1,
        bool fromMe = false,
        bool awaiting = false) =>
        new(key, name, 0, Now.AddDays(-daysAgo), preview, awaiting, fromMe, phone);

    private static IReadOnlyList<ReviewAskCandidate> Select(
        IEnumerable<OversightChatSnapshotService.ChatEntry> chats,
        ISet<string>? asked = null) =>
        ReviewAskCandidates.Select(
            [("acc", "Depilex DHA-2", chats.ToList())],
            asked ?? new HashSet<string>(StringComparer.Ordinal),
            Now);

    // ---- who qualifies ---------------------------------------------------------------------------------

    [Fact]
    public void AGratefulCustomerFromThisWeekQualifies()
    {
        var picked = Assert.Single(Select([Chat("Thank you so much, loved it!")]));
        Assert.Equal("Tayyaba Qasim", picked.CustomerName);
        Assert.Equal("Depilex DHA-2", picked.AccountName);
    }

    [Theory]
    [InlineData("shukriya bohat acha kaam")]
    [InlineData("Jazakallah, great service")]
    [InlineData("Perfect, appreciate it")]
    public void GratitudeIsRecognisedInBothLanguages(string preview) =>
        Assert.Single(Select([Chat(preview)]));

    // ---- who is excluded, and why ----------------------------------------------------------------------

    [Fact]
    public void MerelyClosingTheConversationIsNotGratitude()
    {
        // ReplyNeed treats "ok" and "noted" as closing a chat, and they do — but they are not evidence
        // anyone was pleased. Asking someone for a public review because they said "ok" is how this feature
        // would earn one-star reviews rather than five-star ones.
        Assert.Empty(Select([Chat("ok")]));
        Assert.Empty(Select([Chat("noted")]));
        Assert.Empty(Select([Chat("done")]));
    }

    [Fact]
    public void SomeoneStillWaitingOnAReplyIsNeverAsked()
    {
        // The worst possible moment to ask a favour.
        Assert.Empty(Select([Chat("Thanks! and one more question", awaiting: true)]));
    }

    [Fact]
    public void TheSalonThankingItselfDoesNotCount()
    {
        // If the salon spoke last, "thanks" in the preview is very likely the salon's own.
        Assert.Empty(Select([Chat("Thank you for visiting us!", fromMe: true)]));
    }

    [Fact]
    public void AVisitTooLongAgoIsNotFollowedUp()
    {
        // Beyond two weeks the customer has to work to remember the visit, and the ask reads as a marketing
        // round rather than a follow-up.
        Assert.Empty(Select([Chat("Thank you!", daysAgo: 20)]));
        Assert.Single(Select([Chat("Thank you!", daysAgo: 13)]));
    }

    [Fact]
    public void GroupsAndBroadcastsAreNotCustomers()
    {
        Assert.Empty(Select([Chat("Thanks everyone!", key: "12345@g.us")]));
        Assert.Empty(Select([Chat("Thanks!", key: "status@broadcast")]));
    }

    [Fact]
    public void SomeoneWithNoResolvedPhoneIsStillReachableByConversationKey()
    {
        // Measured on real data: requiring a phone blocked 8 of the 9 non-awaiting chats, because unsaved
        // contacts sit under an @lid privacy id and the number is recovered separately. The conversation key
        // is equally stable, which is all "ask once, ever" needs.
        var picked = Assert.Single(Select([Chat("Thank you!", phone: "", key: "9988@lid")]));
        Assert.Equal("9988@lid", picked.AskKey);
    }

    [Fact]
    public void SomeoneWithNoStableIdentityAtAllIsSkipped()
    {
        // No phone and no conversation key means nothing to remember them by, and a promise that cannot be
        // kept should not be made.
        Assert.Empty(Select([Chat("Thank you!", phone: "", key: "")]));
    }

    [Fact]
    public void AnIdentityAlreadyAskedIsExcludedWhicheverKeyItUsed()
    {
        var asked = new HashSet<string>(StringComparer.Ordinal) { "9988@lid" };
        Assert.Empty(Select([Chat("Thank you!", phone: "", key: "9988@lid")], asked));
    }

    [Fact]
    public void AlreadyAskedMeansNeverAgain()
    {
        var asked = new HashSet<string>(StringComparer.Ordinal) { "923001234567" };
        Assert.Empty(Select([Chat("Thank you!")], asked));
    }

    [Fact]
    public void OnePersonAcrossTwoAccountsIsOneRow()
    {
        // The same customer may have messaged two branches. They are one person and get one ask.
        var chats = new[] { Chat("Thank you!", daysAgo: 3), Chat("Thanks again!", daysAgo: 1) };
        var picked = ReviewAskCandidates.Select(
            [("a", "DHA-2", new[] { chats[0] }), ("b", "F-11", new[] { chats[1] })],
            new HashSet<string>(StringComparer.Ordinal),
            Now);

        var only = Assert.Single(picked);
        Assert.Equal("F-11", only.AccountName);   // the more recent conversation wins
    }

    [Fact]
    public void TheMostRecentVisitsComeFirst()
    {
        var picked = Select([
            Chat("Thanks!", phone: "1", key: "1@c.us", daysAgo: 9),
            Chat("Thank you!", phone: "2", key: "2@c.us", daysAgo: 2)
        ]);

        Assert.Equal(["2", "1"], picked.Select(p => p.Phone));
    }

    // ---- the message -----------------------------------------------------------------------------------

    [Fact]
    public void TheDraftNamesTheCustomerAndTheSalon()
    {
        var text = ReviewAskDraft.Build("Tayyaba Qasim", "Depilex DHA-2");

        Assert.StartsWith("Hi Tayyaba!", text);
        Assert.Contains("Depilex DHA-2", text);
        Assert.Contains("Google", text);
    }

    [Fact]
    public void TheDraftGivesTheCustomerAnEasyOut() =>
        // A request that does not read as optional is pressure applied to someone's phone.
        Assert.Contains("No worries at all", ReviewAskDraft.Build("Ali", "Depilex F-11"));

    [Fact]
    public void NoLinkIsBetterThanAMadeUpOne()
    {
        // A generic or invented link fails silently in the customer's hands, which is worse than asking
        // them to search.
        var withoutLink = ReviewAskDraft.Build("Ali", "Depilex F-11");
        Assert.DoesNotContain("http", withoutLink);

        var withLink = ReviewAskDraft.Build("Ali", "Depilex F-11", "https://g.page/r/abc/review");
        Assert.Contains("https://g.page/r/abc/review", withLink);
    }

    [Fact]
    public void AnUnnamedCustomerGetsAPlainGreeting() =>
        Assert.StartsWith("Hello!", ReviewAskDraft.Build("+923001234567", "Depilex F-11"));

    // ---- the once-ever promise, across restarts --------------------------------------------------------

    [Fact]
    public async Task AnAskIsRememberedAfterAReload()
    {
        // A rule that only holds until the next restart is not a rule.
        var store = new ReviewAskStore(_path);
        await store.MarkAskedAsync("923001234567");

        var reloaded = new ReviewAskStore(_path);
        await reloaded.LoadAsync();

        Assert.Contains("923001234567", reloaded.AskedPhones());
    }

    [Fact]
    public async Task AskedWithinCountsOnlyTheRecentOnes()
    {
        var store = new ReviewAskStore(_path);
        await store.MarkAskedAsync("923001234567");

        Assert.Equal(1, store.AskedWithin(30));
        Assert.Equal(1, store.AskedWithin(0));   // asked today
    }

    [Fact]
    public void AnEmptyStoreExcludesNobody() =>
        Assert.Empty(new ReviewAskStore(_path).AskedPhones());
}

/// <summary>
/// The name the customer sees in a review request.
/// </summary>
public class ReviewAskBusinessNameTests
{
    [Theory]
    [InlineData("Depilex DHA-2 WhatsApp", "Depilex DHA-2")]
    [InlineData("Depilex F-11 WhatsApp", "Depilex F-11")]
    [InlineData("Depilex Men DHA-2 WhatsApp Business", "Depilex Men DHA-2")]
    [InlineData("Depilex DHA-2", "Depilex DHA-2")]
    public void TheChannelIsStrippedFromTheBusinessName(string account, string expected) =>
        // Candidates come from WhatsApp accounts, so the display name carries the channel. Unstripped, the
        // message asked the customer to "leave Depilex DHA-2 WhatsApp a review on Google" — naming a
        // messaging app to someone who only knows the salon.
        Assert.Equal(expected, ReviewAskDraft.BusinessNameFrom(account));

    [Fact]
    public void AnAccountNamedOnlyForItsChannelKeepsThatName() =>
        // "WhatsApp" is the best name that account has; stripping it would leave nothing.
        Assert.Equal("WhatsApp", ReviewAskDraft.BusinessNameFrom("WhatsApp"));

    [Fact]
    public void NoNameAtAllFallsBackToSomethingSayable() =>
        Assert.Equal("us", ReviewAskDraft.BusinessNameFrom(""));

    [Fact]
    public void TheDraftUsesTheStrippedName()
    {
        var text = ReviewAskDraft.Build("Tayyaba Qasim", "Depilex DHA-2 WhatsApp");

        Assert.Contains("Depilex DHA-2 a review on Google", text);
        Assert.DoesNotContain("WhatsApp", text);
    }
}
