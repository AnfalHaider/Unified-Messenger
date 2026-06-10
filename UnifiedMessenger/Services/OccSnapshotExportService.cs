using System.Globalization;
using System.Text;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class OccSnapshotExportService
{
    private static readonly Lazy<OccSnapshotExportService> LazyInstance = new(() => new OccSnapshotExportService());

    private OccSnapshotExportService()
    {
    }

    public static OccSnapshotExportService Instance => LazyInstance.Value;

    internal static OccSnapshotExportService CreateForTests() => new();

    public async Task ExportCsvAsync(
        OperationsCommandCenterSnapshot snapshot,
        string? branchFilterKey,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "Section,Key,Value",
            $"Metadata,ExportedAtUtc,{CsvEscape(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))}",
            $"Metadata,BranchFilter,{CsvEscape(branchFilterKey ?? string.Empty)}",
            $"Metadata,ScopeLabel,{CsvEscape(snapshot.ScopeLabel)}",
            string.Empty,
            "KPI,Value",
            $"OpenThreads,{snapshot.Status.OpenThreadCount.ToString(CultureInfo.InvariantCulture)}",
            $"HangingLeads,{snapshot.Status.HangingLeadCount.ToString(CultureInfo.InvariantCulture)}",
            $"ImmediateActionTotal,{snapshot.Status.ImmediateActionTotal.ToString(CultureInfo.InvariantCulture)}",
            $"ImmediateActionQueueCount,{snapshot.Status.ImmediateActionQueueCount.ToString(CultureInfo.InvariantCulture)}",
            $"RevenueAtRisk,{snapshot.Status.TotalRevenueAtRisk.ToString(CultureInfo.InvariantCulture)}",
            $"AverageReplyTime,{CsvEscape(snapshot.Status.AverageReplyTime)}",
            $"SlaBreaches,{CsvEscape(snapshot.Status.SlaBreaches)}",
            $"ResponseRate,{CsvEscape(snapshot.Status.ResponseRate)}",
            $"PeakHour,{CsvEscape(snapshot.Status.PeakHour)}",
            $"DailyTrend,{CsvEscape(snapshot.Status.DailyTrend)}",
            string.Empty,
            "ThreadId,CustomerName,BranchName,InstanceId,Platform"
        };

        foreach (var thread in snapshot.ThreadOperations.ImmediateActionQueue)
        {
            lines.Add(string.Join(',',
                CsvEscape(thread.ThreadId),
                CsvEscape(thread.CustomerName),
                CsvEscape(thread.BranchName),
                CsvEscape(thread.InstanceId),
                CsvEscape(thread.Platform)));
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllLinesAsync(destinationPath, lines, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
