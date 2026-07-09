using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private sealed record LocalAiModelOption(string ModelId, string Label)
    {
        public override string ToString() => Label;
    }

    private IReadOnlyList<OllamaCatalogModel> _aiCatalog = [];
    private CancellationTokenSource? _aiPullCts;
    private CancellationTokenSource? _aiRuntimeDownloadCts;
    private long _pullPreviousCompleted;
    private DateTimeOffset _pullPreviousAt;
    private long _runtimeDownloadPreviousCompleted;
    private DateTimeOffset _runtimeDownloadPreviousAt;
    private bool _systemOllamaHealthy;

    private void EnsureAiSectionInitialized()
    {
        if (LocalAiModelBox.ItemsSource is not null)
        {
            return;
        }

        _aiCatalog = AiSettingsSectionHelper.LoadCatalog();
        LocalAiModelBox.ItemsSource = _aiCatalog
            .Select(model => new LocalAiModelOption(model.Id, $"{model.DisplayName} ({model.SizeLabel})"))
            .ToList();
        LocalAiModelBox.DisplayMemberPath = nameof(LocalAiModelOption.Label);
        LocalAiModelBox.SelectedValuePath = nameof(LocalAiModelOption.ModelId);
    }

    private void RefreshAiSection()
    {
        EnsureAiSectionInitialized();

        var settings = _services.AppSettings.Settings;
        _suppressToggleEvents = true;
        EnableLocalAiToggle.IsOn = settings.EnableLocalAi;
        OllamaAutoBootstrapToggle.IsOn = settings.OllamaAutoBootstrap;
        OllamaEndpointBox.Text = settings.OllamaEndpoint.TrimEnd('/');
        AiAdvancedPanel.Visibility = settings.EnableLocalAi ? Visibility.Visible : Visibility.Collapsed;
        AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
            _services.OllamaRuntime.ConnectionState);

        var selectedModel = settings.LocalAiModelName;
        LocalAiModelBox.SelectedItem = ((IReadOnlyList<LocalAiModelOption>)LocalAiModelBox.ItemsSource!)
            .FirstOrDefault(option =>
                option.ModelId.Equals(selectedModel, StringComparison.OrdinalIgnoreCase))
            ?? ((IReadOnlyList<LocalAiModelOption>)LocalAiModelBox.ItemsSource!)[0];
        _suppressToggleEvents = false;

        _ = RefreshAiRuntimeUiStateAsync();
    }

    private async Task RefreshAiRuntimeUiStateAsync()
    {
        if (!_services.AppSettings.Settings.EnableLocalAi)
        {
            UpdateRuntimeDownloadButtonVisibility(false);
            return;
        }

        _systemOllamaHealthy = await _services.OllamaRuntime.ProbeSystemOllamaAsync();
        UpdateRuntimeDownloadButtonVisibility(
            AiSettingsSectionHelper.ShouldShowRuntimeDownloadButton(
                enableLocalAi: true,
                hasEmbeddedExecutable: _services.OllamaRuntime.HasEmbeddedExecutable,
                systemOllamaHealthy: _systemOllamaHealthy));
    }

    private void UpdateRuntimeDownloadButtonVisibility(bool visible)
    {
        DownloadAiRuntimeButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void EnableLocalAiToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        var enabled = EnableLocalAiToggle.IsOn;
        if (enabled)
        {
            var confirm = new ContentDialog
            {
                Title = "Download local AI components?",
                Content = AiSettingsSectionHelper.FirstEnableDisclosureMessage,
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                _suppressToggleEvents = true;
                EnableLocalAiToggle.IsOn = false;
                _suppressToggleEvents = false;
                return;
            }
        }

        await _services.AppSettings.UpdateAsync(settings => settings.EnableLocalAi = enabled);
        AiAdvancedPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (enabled)
        {
            AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
                OllamaConnectionState.Starting);
            await RefreshAiRuntimeUiStateAsync();

            if (_systemOllamaHealthy)
            {
                _ = WarmupAiRuntimeAsync();
            }
            else if (_services.OllamaRuntime.HasEmbeddedExecutable)
            {
                _ = WarmupAiRuntimeAsync();
            }
            else if (_services.AppSettings.Settings.OllamaAutoBootstrap)
            {
                await DownloadAiRuntimeCoreAsync(showProgress: true);
            }
            else
            {
                AiConnectionStatusText.Text =
                    "Runtime missing — download the Ollama runtime or start a system install on 127.0.0.1:11434.";
            }
        }
        else
        {
            AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
                OllamaConnectionState.NotRunning);
            UpdateRuntimeDownloadButtonVisibility(false);
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

    private async void LocalAiModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents ||
            LocalAiModelBox.SelectedItem is not LocalAiModelOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.LocalAiModelName = option.ModelId);
    }

    private async void DownloadAiRuntimeButton_Click(object sender, RoutedEventArgs e)
    {
        await DownloadAiRuntimeCoreAsync(showProgress: true);
    }

    private async Task DownloadAiRuntimeCoreAsync(bool showProgress)
    {
        _aiRuntimeDownloadCts?.Cancel();
        _aiRuntimeDownloadCts?.Dispose();
        _aiRuntimeDownloadCts = new CancellationTokenSource();

        if (showProgress)
        {
            SetAiRuntimeControlsEnabled(false);
            AiRuntimeDownloadProgressBar.Visibility = Visibility.Visible;
            AiRuntimeDownloadStatusText.Visibility = Visibility.Visible;
            AiRuntimeDownloadProgressBar.IsIndeterminate = true;
            AiRuntimeDownloadProgressBar.Value = 0;
            AiRuntimeDownloadStatusText.Text = "Downloading Ollama runtime…";
            _runtimeDownloadPreviousCompleted = 0;
            _runtimeDownloadPreviousAt = DateTimeOffset.UtcNow;
        }

        var progress = new Progress<OllamaRuntimeDownloadProgress>(ReportRuntimeDownloadProgress);

        try
        {
            var ok = await _services.OllamaRuntime.DownloadRuntimeAsync(
                progress,
                _aiRuntimeDownloadCts.Token);

            if (ok)
            {
                await _services.OllamaRuntime.EnsureRunningAsync(_aiRuntimeDownloadCts.Token);
                AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
                    _services.OllamaRuntime.ConnectionState);
            }
            else if (showProgress)
            {
                AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
                    _services.OllamaRuntime.ConnectionState);
            }
        }
        catch (OperationCanceledException)
        {
            if (showProgress)
            {
                AiRuntimeDownloadStatusText.Text = "Download cancelled";
            }
        }
        catch (Exception ex)
        {
            if (showProgress)
            {
                AiRuntimeDownloadStatusText.Text = ex.Message;
                AiConnectionStatusText.Text = ex.Message;
            }
        }
        finally
        {
            if (showProgress)
            {
                AiRuntimeDownloadProgressBar.IsIndeterminate = false;
                SetAiRuntimeControlsEnabled(true);
            }

            await RefreshAiRuntimeUiStateAsync();
        }
    }

    private void SetAiRuntimeControlsEnabled(bool enabled)
    {
        DownloadAiRuntimeButton.IsEnabled = enabled;
        TestOllamaConnectionButton.IsEnabled = enabled;
        PullLocalAiModelButton.IsEnabled = enabled;
    }

    private void ReportRuntimeDownloadProgress(OllamaRuntimeDownloadProgress progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (progress.Total > 0)
            {
                AiRuntimeDownloadProgressBar.IsIndeterminate = false;
                AiRuntimeDownloadProgressBar.Value = progress.PercentComplete;
            }
            else if (progress.Phase is "extracting" or "downloading")
            {
                AiRuntimeDownloadProgressBar.IsIndeterminate = true;
            }

            var speed = AiSettingsSectionHelper.TryComputeBytesPerSecond(
                _runtimeDownloadPreviousCompleted,
                progress.Completed,
                _runtimeDownloadPreviousAt,
                DateTimeOffset.UtcNow);
            AiRuntimeDownloadStatusText.Text =
                AiSettingsSectionHelper.FormatRuntimeDownloadProgress(progress, speed);
            _runtimeDownloadPreviousCompleted = progress.Completed;
            _runtimeDownloadPreviousAt = DateTimeOffset.UtcNow;
        });
    }

    private async void TestOllamaConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        TestOllamaConnectionButton.IsEnabled = false;
        PullLocalAiModelButton.IsEnabled = false;
        DownloadAiRuntimeButton.IsEnabled = false;
        AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
            OllamaConnectionState.Starting);

        try
        {
            var ok = await _services.OllamaRuntime.EnsureRunningAsync();
            AiConnectionStatusText.Text = ok
                ? AiSettingsSectionHelper.DescribeConnectionState(OllamaConnectionState.Running)
                : AiSettingsSectionHelper.DescribeConnectionState(_services.OllamaRuntime.ConnectionState);
            await RefreshAiRuntimeUiStateAsync();
        }
        catch (Exception ex)
        {
            AiConnectionStatusText.Text = ex.Message;
        }
        finally
        {
            SetAiRuntimeControlsEnabled(true);
        }
    }

    private async void PullLocalAiModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (LocalAiModelBox.SelectedItem is not LocalAiModelOption option)
        {
            return;
        }

        if (_services.OllamaRuntime.ConnectionState != OllamaConnectionState.Running)
        {
            AiModelPullStatusText.Visibility = Visibility.Visible;
            AiModelPullStatusText.Text = "Start or download the Ollama runtime before pulling a model.";
            return;
        }

        _aiPullCts?.Cancel();
        _aiPullCts?.Dispose();
        _aiPullCts = new CancellationTokenSource();

        TestOllamaConnectionButton.IsEnabled = false;
        PullLocalAiModelButton.IsEnabled = false;
        DownloadAiRuntimeButton.IsEnabled = false;
        AiModelPullProgressBar.Visibility = Visibility.Visible;
        AiModelPullStatusText.Visibility = Visibility.Visible;
        AiModelPullProgressBar.IsIndeterminate = true;
        AiModelPullProgressBar.Value = 0;
        AiModelPullStatusText.Text = $"Pulling {option.ModelId}…";
        _pullPreviousCompleted = 0;
        _pullPreviousAt = DateTimeOffset.UtcNow;

        var progress = new Progress<OllamaPullProgress>(ReportPullProgress);

        try
        {
            var ok = await OllamaModelPullHelper.PullModelAsync(
                _services.OllamaRuntime,
                option.ModelId,
                progress,
                _aiPullCts.Token);

            AiModelPullStatusText.Text = ok
                ? "Model ready"
                : AiSettingsSectionHelper.FormatPullProgress(
                    new OllamaPullProgress
                    {
                        Model = option.ModelId,
                        Status = "failed",
                        Completed = 0,
                        Total = 0,
                        IsComplete = false,
                        Error = "Pull did not complete"
                    },
                    null);
        }
        catch (OperationCanceledException)
        {
            AiModelPullStatusText.Text = "Pull cancelled";
        }
        catch (Exception ex)
        {
            AiModelPullStatusText.Text = ex.Message;
        }
        finally
        {
            AiModelPullProgressBar.IsIndeterminate = false;
            SetAiRuntimeControlsEnabled(true);
        }
    }

    private void ReportPullProgress(OllamaPullProgress progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (progress.Total > 0)
            {
                AiModelPullProgressBar.IsIndeterminate = false;
                AiModelPullProgressBar.Value = progress.PercentComplete;
            }

            var speed = AiSettingsSectionHelper.TryComputeBytesPerSecond(
                _pullPreviousCompleted,
                progress.Completed,
                _pullPreviousAt,
                DateTimeOffset.UtcNow);
            AiModelPullStatusText.Text = AiSettingsSectionHelper.FormatPullProgress(progress, speed);
            _pullPreviousCompleted = progress.Completed;
            _pullPreviousAt = DateTimeOffset.UtcNow;
        });
    }

    private async Task WarmupAiRuntimeAsync()
    {
        try
        {
            await _services.OllamaRuntime.EnsureRunningAsync();
            AiConnectionStatusText.Text = AiSettingsSectionHelper.DescribeConnectionState(
                _services.OllamaRuntime.ConnectionState);
            await RefreshAiRuntimeUiStateAsync();
        }
        catch (Exception ex)
        {
            AiConnectionStatusText.Text = ex.Message;
        }
    }
}
