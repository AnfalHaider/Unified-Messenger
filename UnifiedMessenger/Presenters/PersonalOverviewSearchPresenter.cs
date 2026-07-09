using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Presenters;

public static class PersonalOverviewSearchPresenter
{
    public static IReadOnlyList<PersonalOverviewSearchSuggestionViewModel> BuildSuggestions(
        IEnumerable<MessengerInstance> personalInstances,
        IEnumerable<NotificationAlert> personalAlerts,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(personalInstances);
        ArgumentNullException.ThrowIfNull(personalAlerts);

        return DashboardPageHelper
            .FilterPersonalSearchMatches(personalInstances, query, personalAlerts)
            .Select(match => new PersonalOverviewSearchSuggestionViewModel
            {
                Label = match.Label,
                SubLabel = match.SubLabel,
                InstanceId = match.InstanceId,
                AccentColorHex = match.AccentColorHex
            })
            .ToList();
    }
}
