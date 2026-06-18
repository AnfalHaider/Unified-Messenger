using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class WorkspaceManagementDialog : ContentDialog
{
    private readonly List<WorkspaceProfile> _profiles;
    private int _currentIndex = -1;
    private bool _suppressSelection;

    public WorkspaceManagementDialog(
        IReadOnlyList<MessengerInstance> instances,
        IReadOnlyList<WorkspaceProfile> existing)
    {
        InitializeComponent();
        AlertThresholdBox.Value = AppSettingsService.Instance.Settings.OversightAwaitingAlertThreshold;
        _profiles = WorkspaceManagementHelper.BuildEditableProfiles(instances, existing);

        if (_profiles.Count == 0)
        {
            LocationCombo.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            EmptyHint.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            return;
        }

        _suppressSelection = true;
        foreach (var profile in _profiles)
        {
            LocationCombo.Items.Add(profile.DisplayName);
        }

        LocationCombo.SelectedIndex = 0;
        _suppressSelection = false;
        _currentIndex = 0;
        LoadInto(_profiles[0]);
    }

    private void OnLocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        if (_currentIndex >= 0 && _currentIndex < _profiles.Count)
        {
            SaveEditorInto(_profiles[_currentIndex]);
        }

        _currentIndex = LocationCombo.SelectedIndex;
        if (_currentIndex >= 0 && _currentIndex < _profiles.Count)
        {
            LoadInto(_profiles[_currentIndex]);
        }
    }

    private void LoadInto(WorkspaceProfile profile)
    {
        SlaBox.Value = profile.SlaThresholdMinutes ?? OperationalThresholds.GetSlaThresholdMinutes();
        HoursToggle.IsOn = profile.Hours.Enabled;
        OpenPicker.SelectedTime = TimeSpan.FromMinutes(Math.Clamp(profile.Hours.OpenMinutes, 0, 1439));
        ClosePicker.SelectedTime = TimeSpan.FromMinutes(Math.Clamp(profile.Hours.CloseMinutes, 0, 1439));
    }

    private void SaveEditorInto(WorkspaceProfile profile)
    {
        if (!double.IsNaN(SlaBox.Value) && SlaBox.Value > 0)
        {
            profile.SlaThresholdMinutes = (int)Math.Round(SlaBox.Value);
        }

        profile.Hours.Enabled = HoursToggle.IsOn;
        if (OpenPicker.SelectedTime is TimeSpan open)
        {
            profile.Hours.OpenMinutes = (int)open.TotalMinutes;
        }

        if (ClosePicker.SelectedTime is TimeSpan close)
        {
            profile.Hours.CloseMinutes = (int)close.TotalMinutes;
        }
    }

    private async void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (_currentIndex >= 0 && _currentIndex < _profiles.Count)
            {
                SaveEditorInto(_profiles[_currentIndex]);
            }

            var threshold = double.IsNaN(AlertThresholdBox.Value) ? 0 : (int)Math.Round(AlertThresholdBox.Value);
            await AppSettingsService.Instance.UpdateAsync(settings =>
            {
                settings.WorkspaceProfiles = _profiles;
                settings.OversightAwaitingAlertThreshold = threshold;
            });
        }
        finally
        {
            deferral.Complete();
        }
    }
}
