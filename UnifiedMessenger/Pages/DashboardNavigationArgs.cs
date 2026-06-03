using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed class DashboardNavigationArgs
{
    public required InstanceRegistryService Registry { get; init; }
}
