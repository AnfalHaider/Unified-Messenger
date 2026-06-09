using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;

namespace UnifiedMessenger.ViewModels;

public partial class BranchWorkspacePillBarViewModel : ViewModelBase
{
    public ObservableCollection<BranchWorkspacePillItem> Items { get; } = [];

    [ObservableProperty]
    private string? _selectedBranchKey;

    [ObservableProperty]
    private bool _isInteractionEnabled = true;

    [ObservableProperty]
    private string _pillBarSignature = string.Empty;

    public void ApplyPillBar(OccPillBarPresentation pillBar, string? selectedBranchKey)
    {
        ArgumentNullException.ThrowIfNull(pillBar);

        SelectedBranchKey = selectedBranchKey;
        PillBarSignature = pillBar.Signature;

        Items.Clear();
        foreach (var item in pillBar.Items)
        {
            Items.Add(item);
        }
    }
}
