using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;
using UnifiedMessenger.Services.VoiceNotes;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Pages;

public sealed partial class LocalAISettingsPage : Page
{
    private readonly LocalAISettingsViewModel _viewModel = new();
    private readonly Dictionary<string, LocalAiModelRowViewModel> _rowsByModelId =
        new(StringComparer.OrdinalIgnoreCase);

    private ApplicationServices _services = new();
    private readonly OllamaOrchestrationService _ollama = OllamaOrchestrationService.Instance;
    private DispatcherQueue? _dispatcher;
    private bool _suppressToggleEvents;
    private bool _suppressDraftToneEvents;
    private CancellationTokenSource? _pageCts;
    private string? _activeDownloadModelId;
    private long _lastPullCompleted;
    private DateTimeOffset _lastPullAt = DateTimeOffset.UtcNow;

    public LocalAISettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _pageCts = new CancellationTokenSource();
        _ollama.ConnectionStateChanged += OnConnectionStateChanged;
        _ollama.PullProgressChanged += OnPullProgressChanged;
        BuildModelRows();
        RefreshAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _ollama.ConnectionStateChanged -= OnConnectionStateChanged;
        _ollama.PullProgressChanged -= OnPullProgressChanged;
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ApplicationServices services)
        {
            _services = services;
        }

        _viewModel.BreadcrumbText = SettingsNavigationHelper.BuildBreadcrumb("Local AI");
    }

    private void BuildModelRows()
    {
        _viewModel.ModelRows.Clear();
        _rowsByModelId.Clear();

        foreach (var catalogEntry in LocalAiSettingsPageHelper.LoadCatalog())
        {
            var row = new LocalAiModelRowViewModel(
                catalogEntry.Id,
                catalogEntry.DisplayName,
                catalogEntry.SizeLabel,
                catalogEntry.Description);
            _viewModel.ModelRows.Add(row);
            _rowsByModelId[catalogEntry.Id] = row;
        }

        ModelsList.ItemsSource = _viewModel.ModelRows;
    }

    private void RefreshAll()
    {
        var settings = _services.AppSettings.Settings;

        _suppressToggleEvents = true;
        _viewModel.EnableLocalAi = settings.EnableLocalAi;
        _viewModel.OllamaAutoBootstrap = settings.OllamaAutoBootstrap;
        _viewModel.EnableAutoDraft = settings.EnableAutoDraft;
        _viewModel.EnableBranchPulse = settings.EnableBranchPulse;
        _viewModel.AutoDraftOnlyWhenVisible = settings.AutoDraftOnlyWhenVisible;
        _viewModel.CanRefreshEngine = settings.EnableLocalAi;
        _viewModel.DefaultModelId = settings.LocalAiModelName;
        EnableLocalAiToggle.IsOn = _viewModel.EnableLocalAi;
        OllamaAutoBootstrapToggle.IsOn = _viewModel.OllamaAutoBootstrap;
        OllamaAutoBootstrapToggle.IsEnabled = _viewModel.EnableLocalAi;
        EnableBranchPulseToggle.IsOn = _viewModel.EnableBranchPulse;
        EnableBranchPulseToggle.IsEnabled = _viewModel.EnableLocalAi;
        EnableAutoDraftToggle.IsOn = _viewModel.EnableAutoDraft;
        EnableAutoDraftToggle.IsEnabled = _viewModel.EnableLocalAi;
        AutoDraftOnlyVisibleToggle.IsOn = _viewModel.AutoDraftOnlyWhenVisible;
        AutoDraftOnlyVisibleToggle.IsEnabled = _viewModel.EnableLocalAi && _viewModel.EnableAutoDraft;
        _suppressToggleEvents = false;

        UpdateModelManagerVisibility(_viewModel.EnableLocalAi);
        RefreshDefaultModelBox();
        RefreshDraftToneBox(settings.DraftTonePreference);
        EnableVoiceNoteTranscriptionToggle.IsOn = settings.EnableVoiceNoteTranscription;
        EnableVoiceNoteTranscriptionToggle.IsEnabled = settings.EnableLocalAi;
        WhisperStatusText.Text = WhisperRuntimeProbe.DescribeStatus(settings);
        ApplyConnectionState(_ollama.ConnectionState);
        _ = RefreshEngineAndModelsAsync();
    }

    private void RefreshDraftToneBox(DraftTonePreference selectedTone)
    {
        if (DraftToneBox.Items.Count == 0)
        {
            DraftToneBox.Items.Add(new ComboBoxItem { Content = "Warm (default)", Tag = DraftTonePreference.Warm });
            DraftToneBox.Items.Add(new ComboBoxItem { Content = "Formal English", Tag = DraftTonePreference.Formal });
            DraftToneBox.Items.Add(new ComboBoxItem { Content = "Roman Urdu friendly", Tag = DraftTonePreference.RomanUrdu });
        }

        _suppressDraftToneEvents = true;
        DraftToneBox.IsEnabled = _services.AppSettings.Settings.EnableLocalAi;
        DraftToneBox.SelectedIndex = selectedTone switch
        {
            DraftTonePreference.Formal => 1,
            DraftTonePreference.RomanUrdu => 2,
            _ => 0
        };
        _suppressDraftToneEvents = false;
    }

    private void RefreshDefaultModelBox()
    {
        var catalog = LocalAiSettingsPageHelper.LoadCatalog();
        DefaultModelBox.ItemsSource = catalog;
        DefaultModelBox.DisplayMemberPath = nameof(OllamaCatalogModel.DisplayName);
        DefaultModelBox.SelectedValuePath = nameof(OllamaCatalogModel.Id);

        var selectedId = _services.AppSettings.Settings.LocalAiModelName;
        DefaultModelBox.SelectedValue = catalog
            .FirstOrDefault(model => model.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
            ?.Id ?? catalog.FirstOrDefault()?.Id;
    }

    private async Task RefreshEngineAndModelsAsync()
    {
        if (!_services.AppSettings.Settings.EnableLocalAi)
        {
            return;
        }

        await _ollama.EnsureEngineRunningAsync(_pageCts?.Token ?? CancellationToken.None)
            .ConfigureAwait(false);

        var installed = await _ollama.ListLocalModelsAsync(_pageCts?.Token ?? CancellationToken.None)
            .ConfigureAwait(false);

        RunOnUiThread(() => ApplyInstalledModels(installed));
    }

    private void ApplyInstalledModels(IReadOnlyList<string> installed)
    {
        foreach (var row in _viewModel.ModelRows)
        {
            var isInstalled = LocalAiSettingsPageHelper.IsModelInstalled(row.ModelId, installed);
            row.IsInstalled = isInstalled;
            row.CanDownload = !isInstalled && !row.IsDownloading;
            if (isInstalled && !row.IsDownloading)
            {
                row.Progress = 100;
                row.StatusText = "Ready on this device";
            }
        }
    }

    private void ApplyConnectionState(OllamaConnectionState state)
    {
        _viewModel.ConnectionStatusText = LocalAiSettingsPageHelper.DescribeConnectionStateShort(state);
        _viewModel.EngineStatusText = LocalAiSettingsPageHelper.DescribeConnectionState(state);
        _viewModel.ConnectionIndicatorColorHex = state switch
        {
            OllamaConnectionState.Running => "#107C10",
            OllamaConnectionState.Starting => "#CA8400",
            OllamaConnectionState.Error => "#C42B1C",
            _ => "#787878"
        };

        ConnectionTitleText.Text = _viewModel.ConnectionStatusText;
        ConnectionDetailText.Text = _viewModel.EngineStatusText;
        ConnectionIndicator.Fill = new SolidColorBrush(ColorFromHex(_viewModel.ConnectionIndicatorColorHex));
    }

    private static Windows.UI.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private void UpdateModelManagerVisibility(bool enabled)
    {
        _viewModel.ShowModelManager = enabled;
        ModelManagerPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnConnectionStateChanged(object? sender, OllamaConnectionState state) =>
        RunOnUiThread(() => ApplyConnectionState(state));

    private void OnPullProgressChanged(object? sender, OllamaPullProgress progress) =>
        RunOnUiThread(() => ApplyPullProgress(progress));

    private void ApplyPullProgress(OllamaPullProgress progress)
    {
        var modelKey = _rowsByModelId.ContainsKey(progress.Model)
            ? progress.Model
            : _activeDownloadModelId ?? progress.Model;

        if (!_rowsByModelId.TryGetValue(modelKey, out var row))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var bytesPerSecond = LocalAiSettingsPageHelper.TryComputeBytesPerSecond(
            _lastPullCompleted,
            progress.Completed,
            _lastPullAt,
            now);
        _lastPullCompleted = progress.Completed;
        _lastPullAt = now;

        row.Progress = progress.PercentComplete;
        row.StatusText = LocalAiSettingsPageHelper.FormatPullProgress(progress, bytesPerSecond);

        if (!string.IsNullOrWhiteSpace(progress.Error))
        {
            row.IsDownloading = false;
            row.CanDownload = true;
            _activeDownloadModelId = null;
            return;
        }

        if (progress.IsComplete)
        {
            row.IsDownloading = false;
            row.IsInstalled = true;
            row.CanDownload = false;
            row.Progress = 100;
            _activeDownloadModelId = null;
            _ = RefreshEngineAndModelsAsync();
        }
    }

    private async void EnableLocalAiToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        var enabled = EnableLocalAiToggle.IsOn;
        await _services.AppSettings.UpdateAsync(settings => settings.EnableLocalAi = enabled);
        OllamaAutoBootstrapToggle.IsEnabled = enabled;
        EnableBranchPulseToggle.IsEnabled = enabled;
        EnableAutoDraftToggle.IsEnabled = enabled;
        AutoDraftOnlyVisibleToggle.IsEnabled = enabled && EnableAutoDraftToggle.IsOn;
        DraftToneBox.IsEnabled = enabled;
        EnableVoiceNoteTranscriptionToggle.IsEnabled = enabled;
        WhisperStatusText.Text = WhisperRuntimeProbe.DescribeStatus(_services.AppSettings.Settings);
        UpdateModelManagerVisibility(enabled);

        if (enabled)
        {
            _ = RefreshEngineAndModelsAsync();
            _ollama.WarmupInBackground();
        }
        else
        {
            ApplyConnectionState(OllamaConnectionState.NotRunning);
        }
    }

    private async void OllamaAutoBootstrapToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.OllamaAutoBootstrap = OllamaAutoBootstrapToggle.IsOn);
    }

    private async void EnableBranchPulseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        var enabled = EnableBranchPulseToggle.IsOn;
        await _services.AppSettings.UpdateAsync(settings => settings.EnableBranchPulse = enabled);
    }

    private async void EnableAutoDraftToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        var enabled = EnableAutoDraftToggle.IsOn;
        await _services.AppSettings.UpdateAsync(settings => settings.EnableAutoDraft = enabled);
        AutoDraftOnlyVisibleToggle.IsEnabled =
            _services.AppSettings.Settings.EnableLocalAi && enabled;
    }

    private async void AutoDraftOnlyVisibleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.AutoDraftOnlyWhenVisible = AutoDraftOnlyVisibleToggle.IsOn);
    }

    private async void EnableVoiceNoteTranscriptionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableVoiceNoteTranscription = EnableVoiceNoteTranscriptionToggle.IsOn);
        WhisperStatusText.Text = WhisperRuntimeProbe.DescribeStatus(_services.AppSettings.Settings);
    }

    private async void DraftToneBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDraftToneEvents ||
            DraftToneBox.SelectedItem is not ComboBoxItem { Tag: DraftTonePreference tone })
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings => settings.DraftTonePreference = tone);
    }

    private async void DefaultModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DefaultModelBox.SelectedValue is not string modelId ||
            string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.LocalAiModelName = modelId);
    }

    private void RefreshEngineButton_Click(object sender, RoutedEventArgs e) =>
        _ = RefreshEngineAndModelsAsync();

    private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string modelId } ||
            string.IsNullOrWhiteSpace(modelId) ||
            !_rowsByModelId.TryGetValue(modelId, out var row))
        {
            return;
        }

        if (!_services.AppSettings.Settings.EnableLocalAi)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_activeDownloadModelId))
        {
            return;
        }

        if (!await _ollama.EnsureEngineRunningAsync(_pageCts?.Token ?? CancellationToken.None)
                .ConfigureAwait(true))
        {
            row.StatusText = "Start Ollama before downloading models.";
            return;
        }

        _activeDownloadModelId = modelId;
        _lastPullCompleted = 0;
        _lastPullAt = DateTimeOffset.UtcNow;
        row.IsDownloading = true;
        row.CanDownload = false;
        row.Progress = 0;
        row.StatusText = "Starting download…";

        var success = await Task.Run(async () =>
                await _ollama.PullModelAsync(modelId, _pageCts?.Token ?? CancellationToken.None)
                    .ConfigureAwait(false))
            .ConfigureAwait(true);

        if (!success && _activeDownloadModelId == modelId)
        {
            row.IsDownloading = false;
            row.CanDownload = !row.IsInstalled;
            if (string.IsNullOrWhiteSpace(row.StatusText) ||
                row.StatusText.StartsWith("Starting", StringComparison.Ordinal))
            {
                row.StatusText = "Download failed. Try again.";
            }

            _activeDownloadModelId = null;
        }
    }

    private void SettingsBreadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
    }

    private void BackLink_Click(object sender, RoutedEventArgs e) =>
        SettingsBreadcrumb_Click(sender, e);

    private void RunOnUiThread(Action action)
    {
        if (_dispatcher is null)
        {
            action();
            return;
        }

        _dispatcher.TryEnqueue(() => action());
    }
}
