using System.Text;
using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Runs <see cref="ReplyNeed"/> over a real <c>oversight-snapshot.json</c> and writes a report of what it
/// would close and what it would keep.
///
/// <para>
/// <b>This is a calibration probe, not an assertion.</b> The lexicon it exercises was written against one
/// business's customers — a Pakistani salon whose clients mix English, Roman Urdu and Urdu script in a
/// single message — and the only way to know whether it is tuned or merely plausible is to point it at
/// real traffic and read the exclusions by hand. It <b>skips silently</b> when no snapshot is present, so
/// it never fails on a machine that has no data, and it reads only from the local app-data folder.
/// </para>
/// <para>
/// Set <c>UM_REPLYNEED_SNAPSHOT</c> to point at a snapshot elsewhere. The report goes to
/// <c>%TEMP%\um-replyneed-report.txt</c> — deliberately not to the repo, because it contains customer
/// message text.
/// </para>
/// </summary>
public class ReplyNeedCalibrationProbe
{
    private static string? ResolveSnapshotPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("UM_REPLYNEED_SNAPSHOT");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidate = Path.Combine(local, "UnifiedMessenger", "oversight-snapshot.json");
        return File.Exists(candidate) ? candidate : null;
    }

    [Fact]
    public void ClassifyRealAwaitingPopulationAndWriteReport()
    {
        var path = ResolveSnapshotPath();
        if (path is null)
        {
            return; // no local data on this machine — nothing to calibrate against
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("instances", out var instances))
        {
            return;
        }

        var previews = new List<string>();
        var lastActivity = new List<DateTimeOffset?>();
        foreach (var instance in EnumerateValues(instances))
        {
            // The persisted snapshot names this "chats"; the live scan envelope calls it
            // "conversations". Accept either so the probe keeps working from either source.
            if (!instance.TryGetProperty("chats", out var conversations) &&
                !instance.TryGetProperty("conversations", out conversations))
            {
                continue;
            }

            if (conversations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var chat in conversations.EnumerateArray())
            {
                if (!chat.TryGetProperty("isAwaiting", out var awaiting) ||
                    awaiting.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                previews.Add(chat.TryGetProperty("preview", out var p) ? p.GetString() ?? "" : "");
                lastActivity.Add(
                    chat.TryGetProperty("lastActivityUtc", out var t) &&
                    DateTimeOffset.TryParse(t.GetString(), out var parsed)
                        ? parsed
                        : null);
            }
        }

        var byReason = new Dictionary<ReplyNeedReason, int>();
        var closed = new List<string>();
        var kept = new List<string>();

        foreach (var preview in previews)
        {
            var verdict = ReplyNeed.Classify(preview);
            byReason[verdict.Reason] = byReason.GetValueOrDefault(verdict.Reason) + 1;
            (verdict.NeedsReply ? kept : closed).Add($"[{verdict.Reason}] {Flatten(preview)}");
        }

        var report = new StringBuilder();
        report.AppendLine($"snapshot: {path}");
        report.AppendLine($"awaiting total: {previews.Count}");
        report.AppendLine($"CLOSED by rules: {closed.Count}");
        report.AppendLine($"KEPT:            {kept.Count}");
        report.AppendLine();
        foreach (var pair in byReason.OrderByDescending(p => p.Value))
        {
            report.AppendLine($"  {pair.Value,5}  {pair.Key}");
        }

        // What the owner will actually see on the hero card, computed the same way BuildAwaitingSplit
        // does — the classifier first, then the age split over whatever survives it.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        int needsReply = 0, backlog = 0, autoClosed = 0, unreadable = 0;
        for (var i = 0; i < previews.Count; i++)
        {
            var verdict = ReplyNeed.Classify(previews[i]);
            if (!verdict.NeedsReply)
            {
                autoClosed++;
                continue;
            }

            var isBacklog = lastActivity[i] is { } when && when < cutoff;
            if (isBacklog)
            {
                backlog++;
            }
            else
            {
                needsReply++;
                if (verdict.Reason == ReplyNeedReason.NoPreviewAvailable)
                {
                    unreadable++; // only the LIVE queue — a backlog item nobody can read is a different problem
                }
            }
        }

        report.AppendLine();
        report.AppendLine("=== WHAT THE HERO CARD WILL SAY ===");
        report.AppendLine($"  needs a reply (last 7 days): {needsReply}");
        report.AppendLine($"  backlog (older)            : {backlog}");
        report.AppendLine($"  closed automatically       : {autoClosed}");
        report.AppendLine($"  of the live queue, unreadable: {unreadable}");

        report.AppendLine().AppendLine("=== EVERY EXCLUSION (read these; one false positive fails the gate) ===");
        foreach (var line in closed.OrderBy(l => l, StringComparer.Ordinal))
        {
            report.AppendLine("  " + line);
        }

        report.AppendLine().AppendLine("=== KEPT, excluding unreadable ones ===");
        foreach (var line in kept.Where(l => !l.StartsWith("[NoPreviewAvailable]", StringComparison.Ordinal)))
        {
            report.AppendLine("  " + line);
        }

        // Topic breakdown, so the filter chips can be judged against real traffic rather than guesses.
        var byTopic = new Dictionary<ConversationTopic, List<string>>();
        foreach (var preview in previews)
        {
            var topic = ConversationTopics.Classify(preview);
            if (!byTopic.TryGetValue(topic, out var list))
            {
                byTopic[topic] = list = [];
            }

            list.Add(Flatten(preview));
        }

        report.AppendLine().AppendLine("=== TOPIC BREAKDOWN ===");
        foreach (var pair in byTopic.OrderByDescending(p => p.Value.Count))
        {
            report.AppendLine($"  {pair.Value.Count,5}  {pair.Key}");
        }

        foreach (var topic in new[]
                 {
                     ConversationTopic.AtRisk, ConversationTopic.JobApplicant,
                     ConversationTopic.BusinessOutreach, ConversationTopic.Booking,
                     ConversationTopic.Enquiry
                 })
        {
            if (!byTopic.TryGetValue(topic, out var list))
            {
                continue;
            }

            report.AppendLine().AppendLine($"--- {topic} ({list.Count}) ---");
            foreach (var line in list.Where(l => l.Length > 0).Take(40))
            {
                report.AppendLine("    " + line);
            }
        }

        var outPath = Path.Combine(Path.GetTempPath(), "um-replyneed-report.txt");
        File.WriteAllText(outPath, report.ToString(), Encoding.UTF8);

        // The only hard assertion: the classifier must never close a message that asks something. That is
        // the property the whole design rests on, and it is checked against real traffic here rather than
        // only against examples chosen by the person who wrote the lexicon.
        foreach (var preview in previews)
        {
            if (ReplyNeed.AsksSomething(preview))
            {
                Assert.True(
                    ReplyNeed.Classify(preview).NeedsReply,
                    $"Closed a message that asks something: '{Flatten(preview)}'");
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Value;
            }
        }
    }

    private static string Flatten(string text) =>
        text.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
