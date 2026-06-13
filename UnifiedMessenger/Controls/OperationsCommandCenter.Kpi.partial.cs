using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void OpenThreadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.OpenThreads);

    private void HangingLeadsCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.HangingLeads);

    private void ImmediateActionCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.NeedsAction);

    private void SlaBreachesCard_Activated(object sender, TappedRoutedEventArgs e) =>
        NavigateKpi(OccKpiKind.SlaBreaches);

    private void NavigateKpi(OccKpiKind kind)
    {
        var threads = _snapshot.ThreadOperations.AllThreads;
        var target = OccKpiNavigationHelper.ResolveTarget(kind, threads);
        if (target is null)
        {
            return;
        }

        _services.Navigation.OpenInstance(
            target.InstanceId,
            target.ConversationKey,
            target.CustomerName);
    }
}
