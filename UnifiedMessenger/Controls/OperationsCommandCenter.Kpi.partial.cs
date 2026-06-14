using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void OpenThreadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.OpenThreads);

    private void HangingLeadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.HangingLeads);

    private void UrgentCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.Urgent);

    private void SlaBreachesCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.SlaBreaches);

    private async void NavigateKpi(OccKpiKind kind)
    {
        var threads = _snapshot.ThreadOperations.AllThreads;
        var target = OccKpiNavigationHelper.ResolveTarget(kind, threads);
        if (target is null)
        {
            return;
        }

        SetWorkspaceLoadingVisible(true);
        try
        {
            await ConversationNavigationCoordinator.NavigateToThreadAsync(
                _services.SessionManager,
                _services.Registry,
                _services.ThreadRegistry,
                _services.Navigation,
                target.InstanceId,
                target.ConversationKey,
                target.CustomerName,
                target.ThreadId).ConfigureAwait(true);
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
        }
    }
}
