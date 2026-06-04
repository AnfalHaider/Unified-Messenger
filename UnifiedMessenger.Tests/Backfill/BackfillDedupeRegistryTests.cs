using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Tests.Backfill;

public class BackfillDedupeRegistryTests
{
    [Fact]
    public void TryAccept_SuppressesDuplicateWithinWindow()
    {
        BackfillDedupeRegistry.ClearForTests();

        Assert.True(BackfillDedupeRegistry.TryAccept("inst-1", "whatsapp", "Sara", "Need help with my order today"));
        Assert.False(BackfillDedupeRegistry.TryAccept("inst-1", "whatsapp", "Sara", "Need help with my order today"));
    }

    [Fact]
    public void BuildKey_IsStableForNormalizedWhitespace()
    {
        BackfillDedupeRegistry.ClearForTests();

        var keyA = BackfillDedupeRegistry.BuildKey("inst-1", "WhatsApp", " Sara ", "hello   world");
        var keyB = BackfillDedupeRegistry.BuildKey("inst-1", "whatsapp", "Sara", "hello world");

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void TryAccept_AllowsDifferentMessageBodies()
    {
        BackfillDedupeRegistry.ClearForTests();

        Assert.True(BackfillDedupeRegistry.TryAccept("inst-1", "whatsapp", "Sara", "First unread preview body text"));
        Assert.True(BackfillDedupeRegistry.TryAccept("inst-1", "whatsapp", "Sara", "Second unread preview body text"));
    }
}
