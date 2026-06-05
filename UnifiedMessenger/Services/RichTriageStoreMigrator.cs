using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Normalizes legacy <c>triage_v2.json</c> payloads and builds branch catalog metadata.
/// </summary>
public static class RichTriageStoreMigrator
{
    public static RichTriageStoreFile Migrate(RichTriageStoreFile? store)
    {
        store ??= new RichTriageStoreFile();

        if (store.Version < 1)
        {
            store.Version = 1;
        }

        store.Items ??= [];
        store.Threads ??= [];
        store.Metadata ??= new UnifiedMessengerStoreMetadata();

        if (store.Version < RichTriageStoreFile.CurrentVersion)
        {
            BackfillThreadsFromItems(store);
            store.Version = RichTriageStoreFile.CurrentVersion;
        }

        store.Metadata.Branches = BuildBranchCatalog(store.Items, store.Threads);
        store.Metadata.SavedAtUtc = DateTimeOffset.UtcNow;
        return store;
    }

    internal static void BackfillThreadsFromItems(RichTriageStoreFile store)
    {
        if (store.Threads.Count > 0 || store.Items.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in store.Items.OrderByDescending(i => i.TimestampUtc))
        {
            var conversationKey = string.IsNullOrWhiteSpace(item.ConversationKey)
                ? item.CustomerName
                : item.ConversationKey;
            var threadId = ThreadRegistryService.BuildThreadId(item.InstanceId, conversationKey);
            if (!seen.Add(threadId))
            {
                continue;
            }

            store.Threads.Add(new ThreadData
            {
                ThreadId = threadId,
                Platform = PlatformDefinition.NormalizePlatformId(item.Platform),
                InstanceId = item.InstanceId,
                InstanceDisplayName = item.InstanceDisplayName,
                BranchName = string.IsNullOrWhiteSpace(item.BranchName)
                    ? BranchNameResolver.Resolve(item.InstanceDisplayName)
                    : item.BranchName,
                CustomerName = item.CustomerName,
                ConversationKey = conversationKey,
                LastMessageTime = item.TimestampUtc,
                LastTriageItemId = item.Id,
                AiIntentCategory = item.AiIntentCategory,
                ClientSentiment = item.ClientSentiment,
                UrgencyScore = item.OperationalUrgency > 0
                    ? item.OperationalUrgency
                    : ThreadRegistryService.MapOperationalUrgency(item.UrgencyScore),
                NextActionSummary = string.IsNullOrWhiteSpace(item.NextActionSummary)
                    ? item.CoreSummary
                    : item.NextActionSummary,
                EstimatedValue = item.EstimatedValue,
                IsRevenueLeakageRisk = item.IsRevenueLeakageRisk
            });
        }
    }

    internal static List<UnifiedMessengerBranchRecord> BuildBranchCatalog(
        IEnumerable<MessageTriageItem> items,
        IEnumerable<ThreadData> threads)
    {
        var catalog = new Dictionary<string, UnifiedMessengerBranchRecord>(StringComparer.OrdinalIgnoreCase);

        void AddRecord(string? branchName, string platform, string instanceId, string displayName)
        {
            var branch = string.IsNullOrWhiteSpace(branchName)
                ? BranchNameResolver.Resolve(displayName)
                : branchName.Trim();
            if (string.IsNullOrWhiteSpace(branch))
            {
                branch = "General";
            }

            var normalizedPlatform = PlatformDefinition.NormalizePlatformId(platform);
            var key = $"{branch}|{normalizedPlatform}|{instanceId}";
            if (catalog.ContainsKey(key))
            {
                return;
            }

            catalog[key] = new UnifiedMessengerBranchRecord
            {
                BranchName = branch,
                Platform = normalizedPlatform,
                InstanceId = instanceId,
                InstanceDisplayName = displayName
            };
        }

        foreach (var item in items)
        {
            AddRecord(item.BranchName, item.Platform, item.InstanceId, item.InstanceDisplayName);
        }

        foreach (var thread in threads)
        {
            AddRecord(thread.BranchName, thread.Platform, thread.InstanceId, thread.InstanceDisplayName);
        }

        return catalog.Values
            .OrderBy(record => record.BranchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Platform, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
