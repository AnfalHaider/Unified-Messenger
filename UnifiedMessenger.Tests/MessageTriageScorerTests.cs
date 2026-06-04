using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class MessageTriageScorerTests
{
    [Fact]
    public void ScoreUrgency_BookingAndCancelKeywordsScoreHigh()
    {
        var score = MessageTriageScorer.ScoreUrgency(
            "I need to reschedule my appointment for tomorrow morning.",
            "Depilex F-11 booking");

        Assert.True(score >= 55);
    }

    [Fact]
    public void ClassifySentiment_DetectsNegativeTone()
    {
        var sentiment = MessageTriageScorer.ClassifySentiment(
            "Very disappointed with the late delivery and damaged product.");

        Assert.Equal(MessageSentiment.Negative, sentiment);
    }

    [Fact]
    public void ClassifySentiment_DetectsPositiveTone()
    {
        var sentiment = MessageTriageScorer.ClassifySentiment(
            "Thank you so much, the team was amazing and very helpful.");

        Assert.Equal(MessageSentiment.Positive, sentiment);
    }
}
