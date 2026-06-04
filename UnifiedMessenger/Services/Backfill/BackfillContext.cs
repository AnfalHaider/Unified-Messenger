using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public sealed class BackfillContext
{
    public required MessengerInstance Instance { get; init; }

    public bool EnableLocalAi { get; init; }

    public int MaxAiInferenceJobs { get; init; } = BackfillSyncManager.DefaultMaxAiInferenceJobs;

    public int SlaThresholdMinutes { get; init; }

    public bool IsBackfill => true;

    internal int AiInferenceJobsScheduled { get; set; }

    public bool TryConsumeAiInferenceSlot()
    {
        if (!EnableLocalAi)
        {
            return false;
        }

        if (AiInferenceJobsScheduled >= MaxAiInferenceJobs)
        {
            return false;
        }

        AiInferenceJobsScheduled++;
        return true;
    }
}
