using OllamaSharp;
using OllamaSharp.Models;
using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

/// <summary>
/// Settings UI helper for pulling Ollama models with progress (not inference pipeline).
/// </summary>
public static class OllamaModelPullHelper
{
    public static async Task<bool> PullModelAsync(
        OllamaRuntimeService runtime,
        string modelName,
        IProgress<OllamaPullProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        if (!await runtime.EnsureRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var endpoint = OllamaOptions.NormalizeEndpoint(AppSettingsService.Instance.Settings.OllamaEndpoint);
        using var client = new OllamaApiClient(new Uri(endpoint));

        try
        {
            await foreach (var status in client
                               .PullModelAsync(modelName.Trim(), cancellationToken)
                               .ConfigureAwait(false))
            {
                var pullProgress = MapProgress(modelName, status);
                progress?.Report(pullProgress);

                if (!string.IsNullOrWhiteSpace(pullProgress.Error))
                {
                    return false;
                }

                if (pullProgress.IsComplete)
                {
                    return true;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            progress?.Report(new OllamaPullProgress
            {
                Model = modelName,
                Status = "pull failed",
                Completed = 0,
                Total = 0,
                IsComplete = false,
                Error = ex.Message
            });
            return false;
        }
    }

    public static async Task<IReadOnlyList<string>> ListInstalledModelsAsync(
        OllamaRuntimeService runtime,
        CancellationToken cancellationToken = default)
    {
        if (!await runtime.EnsureRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var endpoint = OllamaOptions.NormalizeEndpoint(AppSettingsService.Instance.Settings.OllamaEndpoint);
        using var client = new OllamaApiClient(new Uri(endpoint));

        try
        {
            var models = await client.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);
            return models
                .Select(model => model.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama list models failed: {ex.Message}");
            return [];
        }
    }

    private static OllamaPullProgress MapProgress(string modelName, PullModelResponse? status)
    {
        if (status is null)
        {
            return OllamaPullProgress.FromStatus(modelName, "working");
        }

        var completed = status.Completed;
        var total = status.Total;
        var statusText = status.Status ?? string.Empty;
        var isComplete = statusText.Contains("success", StringComparison.OrdinalIgnoreCase);

        return new OllamaPullProgress
        {
            Model = modelName,
            Status = statusText,
            Completed = completed,
            Total = total,
            IsComplete = isComplete,
            Error = null
        };
    }
}
