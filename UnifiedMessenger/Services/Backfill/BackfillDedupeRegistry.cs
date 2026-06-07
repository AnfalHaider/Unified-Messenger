using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public static class BackfillDedupeRegistry
{
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(15);

    private static readonly ConcurrentDictionary<string, DateTimeOffset> Seen =
        new(StringComparer.OrdinalIgnoreCase);

    public static string BuildKey(
        string instanceId,
        string platform,
        string conversationKey,
        string messageText)
    {
        var normalizedPlatform = PlatformDefinition.NormalizePlatformId(platform);
        var conversation = Normalize(conversationKey);
        var message = Normalize(messageText);
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();

        return $"{instanceId.Trim()}|{normalizedPlatform}|{conversation}|{signature}";
    }

    public static bool TryAccept(
        string instanceId,
        string platform,
        string conversationKey,
        string messageText)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        var key = BuildKey(instanceId, platform, conversationKey, messageText);
        var now = DateTimeOffset.UtcNow;

        if (Seen.TryGetValue(key, out var lastSeen) && now - lastSeen < DuplicateWindow)
        {
            return false;
        }

        Seen[key] = now;
        PruneStaleEntries(now);
        return true;
    }

    public static void Clear() => Seen.Clear();

    internal static void ClearForTests() => Clear();

    private static void PruneStaleEntries(DateTimeOffset now)
    {
        if (Seen.Count < 512)
        {
            return;
        }

        var cutoff = now - DuplicateWindow;
        foreach (var pair in Seen)
        {
            if (pair.Value < cutoff)
            {
                Seen.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
