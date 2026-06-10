using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private readonly Dictionary<string, OccPanelChrome> _panelChromes = new(StringComparer.OrdinalIgnoreCase);
    private OccDashboardLayoutPreferences? _layoutUndoSnapshot;
    private string? _layoutDragSourcePanelId;
    private string? _kpiDragSourceMetricId;
    private string? _keyboardMovePanelId;
    private DispatcherQueueTimer? _layoutUndoTimer;
    private bool _suppressPresetSelection;
    private string? _dragOverTargetPanelId;
    private int _dragOverStableTicks;
    private FrameworkElement? _dragPreviewHighlightTarget;
    private Brush? _dragPreviewOriginalBorderBrush;
    private Thickness _dragPreviewOriginalBorderThickness;
    private const int DropPreviewHysteresisTicks = 2;
    private readonly OccLayoutInteractionService _layoutInteraction = new();
}
