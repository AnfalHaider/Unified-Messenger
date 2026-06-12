using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Presenters;

namespace UnifiedMessenger.ViewModels;

public partial class OperationsCommandCenterViewModel : ViewModelBase
{
    public BranchWorkspacePillBarViewModel PillBar { get; } = new();

    public ObservableCollection<OperationsThreadCardViewModel> ImmediateQueue { get; } = [];

    public ObservableCollection<OperationsThreadCardViewModel> NewInquiries { get; } = [];

    public ObservableCollection<OperationsThreadCardViewModel> HangingLeads { get; } = [];

    public ObservableCollection<OperationsThreadCardViewModel> Resolved { get; } = [];

    [ObservableProperty]
    private bool _showWorkspaceLoading;

    [ObservableProperty]
    private bool _isLayoutEditMode;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private bool _showMainContent = true;

    [ObservableProperty]
    private string _scopeLabel = string.Empty;

    [ObservableProperty]
    private string _lastRefreshedText = string.Empty;

    [ObservableProperty]
    private bool _isRefreshInProgress;

    [ObservableProperty]
    private bool _showNewInquiriesEmpty;

    [ObservableProperty]
    private bool _showHangingLeadsEmpty;

    [ObservableProperty]
    private bool _showResolvedEmpty;

    [ObservableProperty]
    private bool _showImmediateQueueEmpty;

    [ObservableProperty]
    private bool _showImmediateQueueFooter;

    [ObservableProperty]
    private string? _immediateQueueFooterText;

    public void ApplyShellPresentation(OccShellPresentation shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        ShowEmptyState = shell.ShowEmptyState;
        ShowMainContent = shell.ShowMainContent;
        ScopeLabel = shell.ScopeLabel;
        LastRefreshedText = shell.LastRefreshedText;
    }

    public void ApplyImmediateQueuePresentation(OccImmediateQueuePresentation queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        ShowImmediateQueueEmpty = queue.ShowEmptyState;
        ShowImmediateQueueFooter = queue.ShowFooter;
        ImmediateQueueFooterText = queue.FooterText;
    }

    public void ApplyKanbanEmptyStates(int newCount, int hangingCount, int resolvedCount)
    {
        ShowNewInquiriesEmpty = newCount == 0;
        ShowHangingLeadsEmpty = hangingCount == 0;
        ShowResolvedEmpty = resolvedCount == 0;
    }
}
