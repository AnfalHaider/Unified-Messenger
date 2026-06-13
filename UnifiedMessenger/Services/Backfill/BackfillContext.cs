using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public sealed class BackfillContext
{
    public required MessengerInstance Instance { get; init; }

    public int SlaThresholdMinutes { get; init; }

    public WhatsAppBackfillMode BackfillMode { get; init; } = WhatsAppBackfillMode.Unread;

    public int BackfillRecentDays { get; init; } = 7;

    public int BackfillMaxChats { get; init; } = WhatsAppBackfillProvider.DefaultMaxChats;

    public bool EnableDeepBackfill { get; init; }

    public bool IsBackfill => true;
}
