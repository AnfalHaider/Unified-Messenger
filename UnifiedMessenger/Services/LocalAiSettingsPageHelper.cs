using System.Globalization;
using System.Text.Json;
using UnifiedMessenger.Models.Ollama;

namespace UnifiedMessenger.Services;

public static class LocalAiSettingsPageHelper
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<OllamaCatalogModel> LoadCatalog(string? baseDirectory = null)
    {
        try
        {
            var root = baseDirectory ?? AppContext.BaseDirectory;
            var path = Path.Combine(root, "Assets", "Config", "ollama-models.json");
            if (!File.Exists(path))
            {
                return DefaultCatalog();
            }

            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<OllamaCatalogDocument>(json, CatalogJsonOptions);
            if (document?.Models is null || document.Models.Count == 0)
            {
                return DefaultCatalog();
            }

            return document.Models
                .Where(static model => !string.IsNullOrWhiteSpace(model.Id))
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load Ollama catalog: {ex.Message}");
            return DefaultCatalog();
        }
    }

    public static string DescribeConnectionState(OllamaConnectionState state) =>
        state switch
        {
            OllamaConnectionState.Running => "Connected — Ollama is ready on 127.0.0.1:11434",
            OllamaConnectionState.Starting => "Starting — checking local engine…",
            OllamaConnectionState.NotRunning => "Not running — enable Local AI or start Ollama",
            OllamaConnectionState.Error => "Error — engine unreachable (see logs)",
            _ => "Unknown — refresh to probe localhost:11434"
        };

    public static string DescribeConnectionStateShort(OllamaConnectionState state) =>
        state switch
        {
            OllamaConnectionState.Running => "Connected",
            OllamaConnectionState.Starting => "Starting",
            OllamaConnectionState.NotRunning => "Offline",
            OllamaConnectionState.Error => "Error",
            _ => "Unknown"
        };

    public static bool IsModelInstalled(string modelId, IReadOnlyList<string> localModels) =>
        localModels.Any(name => ModelNamesMatch(modelId, name));

    public static bool ModelNamesMatch(string catalogId, string installedName)
    {
        if (string.IsNullOrWhiteSpace(catalogId) || string.IsNullOrWhiteSpace(installedName))
        {
            return false;
        }

        if (installedName.Equals(catalogId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedCatalog = catalogId.Split(':', 2)[0];
        return installedName.StartsWith(normalizedCatalog, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatPullProgress(
        OllamaPullProgress progress,
        double? bytesPerSecond)
    {
        if (!string.IsNullOrWhiteSpace(progress.Error))
        {
            return progress.Error;
        }

        if (progress.IsComplete)
        {
            return "Download complete";
        }

        var status = string.IsNullOrWhiteSpace(progress.Status) ? "Working…" : progress.Status;
        if (progress.Total <= 0)
        {
            return status;
        }

        var percent = progress.PercentComplete.ToString("F0", CultureInfo.InvariantCulture);
        var speed = bytesPerSecond is > 0
            ? FormatBytesPerSecond(bytesPerSecond.Value)
            : null;

        return speed is null
            ? $"{status} — {percent}%"
            : $"{status} — {percent}% · {speed}";
    }

    public static double? TryComputeBytesPerSecond(
        long previousCompleted,
        long currentCompleted,
        DateTimeOffset previousAt,
        DateTimeOffset currentAt)
    {
        var elapsedSeconds = (currentAt - previousAt).TotalSeconds;
        if (elapsedSeconds <= 0.05 || currentCompleted < previousCompleted)
        {
            return null;
        }

        var delta = currentCompleted - previousCompleted;
        return delta / elapsedSeconds;
    }

    public static string FormatBytesPerSecond(double bytesPerSecond)
    {
        const double kilo = 1024;
        const double mega = kilo * 1024;
        if (bytesPerSecond >= mega)
        {
            return $"{bytesPerSecond / mega:0.0} MB/s";
        }

        if (bytesPerSecond >= kilo)
        {
            return $"{bytesPerSecond / kilo:0.0} KB/s";
        }

        return $"{bytesPerSecond:0} B/s";
    }

    private static IReadOnlyList<OllamaCatalogModel> DefaultCatalog() =>
    [
        new OllamaCatalogModel
        {
            Id = "phi3:mini",
            DisplayName = "Phi-3 Mini",
            SizeLabel = "2.3 GB",
            Description = "Fast, lightweight replies."
        },
        new OllamaCatalogModel
        {
            Id = "llama3.2",
            DisplayName = "Llama 3.2 8B",
            SizeLabel = "4.7 GB",
            Description = "Balanced drafting quality."
        }
    ];
}
