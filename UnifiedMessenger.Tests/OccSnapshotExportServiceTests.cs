using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OccSnapshotExportServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public OccSnapshotExportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ExportCsvAsync_WritesKpisBranchFilterAndImmediateQueue()
    {
        var snapshot = new OperationsCommandCenterSnapshot
        {
            ScopeLabel = "Showing: F-11",
            Status = new OperationsStatusSnapshot
            {
                OpenThreadCount = 12,
                HangingLeadCount = 3,
                ImmediateActionCount = 2,
                ImmediateActionQueueCount = 2,
                TotalRevenueAtRisk = 1500,
                AverageReplyTime = "4m",
                SlaBreaches = "1",
                ResponseRate = "92%",
                PeakHour = "10:00",
                DailyTrend = "+5%"
            },
            ThreadOperations = new UnifiedMessengerDashboardSnapshot
            {
                ImmediateActionQueue =
                [
                    new ThreadData
                    {
                        ThreadId = "thread-1",
                        CustomerName = "Alex",
                        BranchName = "F-11",
                        InstanceId = "inst-1",
                        Platform = "whatsapp"
                    }
                ]
            }
        };

        var exportPath = Path.Combine(_tempDirectory, "occ-export.csv");
        var service = OccSnapshotExportService.CreateForTests();

        await service.ExportCsvAsync(snapshot, "F-11", exportPath);

        var lines = await File.ReadAllLinesAsync(exportPath);
        Assert.Contains(lines, line => line.Contains("Metadata,BranchFilter,F-11", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("OpenThreads,12", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("ThreadId,CustomerName,BranchName,InstanceId,Platform", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("thread-1,Alex,F-11,inst-1,whatsapp", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
