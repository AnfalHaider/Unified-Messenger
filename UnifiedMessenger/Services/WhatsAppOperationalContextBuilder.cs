using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Builds structured operational reference data injected into WhatsApp-specific Ollama prompts.
/// </summary>
public static class WhatsAppOperationalContextBuilder
{
    public static string BuildOperationalReferenceBlock(
        string instanceDisplayName,
        string? branchKey,
        WhatsAppConversationMetadata? metadata,
        string conversationKey,
        string instanceId)
    {
        var branch = ResolveBranchProfile(branchKey, instanceDisplayName);
        var history = BuildCustomerHistoryBlock(instanceId, conversationKey);
        var labels = metadata?.BusinessLabels is { Count: > 0 }
            ? string.Join(", ", metadata.BusinessLabels)
            : "none";

        var contactLine = BuildContactLine(metadata);

        return $"""
            === BRANCH OPERATIONS ===
            Branch: {branch.BranchKey}
            Services: {string.Join(", ", branch.Services)}
            Standard packages: {string.Join("; ", branch.StandardPackages)}
            Booking rules: {branch.BookingRules}

            === ACTIVE THREAD METADATA ===
            WhatsApp labels: {labels}
            {contactLine}

            === CUSTOMER HISTORY (local store) ===
            {history}
            """;
    }

    public static bool IsWhatsAppPlatform(string? platform) =>
        platform?.Equals("whatsapp", StringComparison.OrdinalIgnoreCase) == true ||
        platform?.Equals("whatsappbusiness", StringComparison.OrdinalIgnoreCase) == true;

    private static BranchOperationalProfile ResolveBranchProfile(string? branchKey, string instanceDisplayName)
    {
        var resolved = BranchWorkspaceHelper.ResolveBranchKey(branchKey, instanceDisplayName);
        var profiles = BuildProfileLookup();

        if (profiles.TryGetValue(resolved, out var profile))
        {
            return profile;
        }

        foreach (var pair in profiles)
        {
            if (resolved.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return BranchOperationalCatalogDefaults.CreateFallbackProfile(resolved);
    }

    private static IReadOnlyDictionary<string, BranchOperationalProfile> BuildProfileLookup()
    {
        var catalog = AppSettingsService.Instance.Settings.BranchOperationalCatalog;
        return catalog
            .Where(profile => !string.IsNullOrWhiteSpace(profile.BranchKey))
            .ToDictionary(
                profile => profile.BranchKey.Trim(),
                profile => profile,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildCustomerHistoryBlock(string instanceId, string conversationKey)
    {
        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, conversationKey);
        var thread = ThreadRegistryService.Instance.GetAllThreads()
            .FirstOrDefault(t => t.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase));
        var priorTriage = BuildPriorTriageHistory(instanceId, conversationKey, threadId);

        if (thread is null && string.IsNullOrWhiteSpace(priorTriage))
        {
            return "New or unknown customer — no prior thread record in local store.";
        }

        if (thread is null)
        {
            return priorTriage;
        }

        var visitHint = thread.IsReplied ? "Prior manager reply logged" : "Awaiting first reply";
        var latency = thread.LatencyMinutes > 0
            ? $"{thread.LatencyMinutes:F0} min since last inbound"
            : "fresh thread";

        return $"""
            Returning customer: yes
            Last intent: {thread.AiIntentCategory}
            Sentiment: {thread.ClientSentiment}
            Thread status: {visitHint}; {latency}
            Estimated thread value: PKR {thread.EstimatedValue:F0}
            {priorTriage}
            """;
    }

    private static string BuildPriorTriageHistory(
        string instanceId,
        string conversationKey,
        string threadId)
    {
        var priorItems = MessageTriageService.Instance.GetAllItems()
            .Where(item =>
                item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase) &&
                (item.ConversationKey.Equals(conversationKey, StringComparison.OrdinalIgnoreCase) ||
                 item.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.TimestampUtc)
            .Take(2)
            .ToList();

        if (priorItems.Count == 0)
        {
            return string.Empty;
        }

        var lines = priorItems.Select(item =>
        {
            var tags = item.IntentTags.Count > 0
                ? $" tags={string.Join(",", item.IntentTags)}"
                : string.Empty;
            var subIntent = string.IsNullOrWhiteSpace(item.SubIntent)
                ? string.Empty
                : $" subIntent={item.SubIntent}";
            return $"- {item.TimestampUtc:yyyy-MM-dd HH:mm} intent={item.AiIntentCategory}{subIntent} urgency={item.OperationalUrgency}{tags}: {item.NextActionSummary}";
        });

        return "Prior triage (local):\n" + string.Join("\n", lines);
    }

    private static string BuildContactLine(WhatsAppConversationMetadata? metadata)
    {
        if (metadata is null)
        {
            return "Verified business name: unknown; contact phone: unknown";
        }

        var verified = string.IsNullOrWhiteSpace(metadata.VerifiedBusinessName)
            ? "unknown"
            : metadata.VerifiedBusinessName.Trim();
        var phone = string.IsNullOrWhiteSpace(metadata.ContactPhoneNumber)
            ? metadata.ProfilePhoneNumber ?? "unknown"
            : metadata.ContactPhoneNumber.Trim();

        return $"Verified business name: {verified}; contact phone: {phone}";
    }
}
