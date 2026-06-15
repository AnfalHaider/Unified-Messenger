using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Presenters;

public static class SettingsImportExportPresenter
{
    public static SettingsExportSummary BuildExportSummary(
        IReadOnlyList<MessengerInstance> activeInstances,
        IReadOnlyList<MessengerInstance> archivedInstances,
        string storePath)
    {
        ArgumentNullException.ThrowIfNull(activeInstances);
        ArgumentNullException.ThrowIfNull(archivedInstances);

        return new SettingsExportSummary(
            activeInstances.Count,
            archivedInstances.Count,
            SettingsPageHelper.ResolveInstancesStorePath(storePath));
    }

    public static string BuildPreExportDialogContent(SettingsExportSummary summary) =>
        SettingsPageHelper.BuildExportPreSummary(summary.ActiveCount, summary.ArchivedCount) +
        Environment.NewLine + Environment.NewLine +
        $"Registry file: {summary.StorePath}";

    public static string BuildImportDialogContent(SettingsImportSummary summary) =>
        SettingsPageHelper.BuildImportConfirmationMessage(
            summary.ActiveCount,
            summary.ArchivedCount);

    public static SettingsImportSummary BuildImportSummary(string sourcePath, InstanceStore imported) =>
        new(
            imported.Instances.Count,
            imported.ArchivedInstances.Count,
            sourcePath);
}
