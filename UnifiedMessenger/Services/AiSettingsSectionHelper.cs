using System.Globalization;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ollama;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Services;

public static class AiSettingsSectionHelper
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultEndpointDisplay => "http://127.0.0.1:11434";

    public const string FirstEnableDisclosureMessage =
        "Local AI requires downloading about 1.4 GB for the Ollama runtime and about 2.2 GB for the default model (phi3:mini). Downloads happen from GitHub to your PC only — nothing leaves your device. Continue?";

    public static string FormatRuntimeDownloadProgress(
        OllamaRuntimeDownloadProgress progress,
        double? bytesPerSecond)
    {
        if (!string.IsNullOrWhiteSpace(progress.Error))
        {
            return progress.Error;
        }

        if (progress.IsComplete)
        {
            return "Runtime ready";
        }

        return progress.Phase switch
        {
            "extracting" => "Extracting Ollama runtime…",
            "downloading" when progress.Total > 0 =>
                FormatPullProgress(
                    new OllamaPullProgress
                    {
                        Model = "runtime",
                        Status = "Downloading runtime",
                        Completed = progress.Completed,
                        Total = progress.Total,
                        IsComplete = false
                    },
                    bytesPerSecond),
            "downloading" => "Downloading Ollama runtime…",
            _ => "Preparing Ollama runtime…"
        };
    }

    public static bool ShouldShowRuntimeDownloadButton(
        bool enableLocalAi,
        bool hasEmbeddedExecutable,
        bool systemOllamaHealthy) =>
        enableLocalAi && !hasEmbeddedExecutable && !systemOllamaHealthy;

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
            OllamaConnectionState.Running =>
                "Connected — Ollama is ready on 127.0.0.1:11434",
            OllamaConnectionState.Starting => "Starting — checking local engine…",
            OllamaConnectionState.NotRunning =>
                "Not running — enable local AI or start Ollama",
            OllamaConnectionState.Error => "Error — engine unreachable (see logs)",
            _ => "Unknown — test connection to probe localhost:11434"
        };

    public static string DescribeOccAiChip(
        AppSettings settings,
        OllamaConnectionState connectionState)
    {
        if (!settings.EnableLocalAi)
        {
            return "AI offline";
        }

        return connectionState == OllamaConnectionState.Running
            ? "AI ready"
            : "AI offline";
    }

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
            SizeLabel = "2.2 GB",
            Description = "Default — fast structured summaries for support threads."
        },
        new OllamaCatalogModel
        {
            Id = "llama3.2:3b",
            DisplayName = "Llama 3.2 3B",
            SizeLabel = "2.0 GB",
            Description = "Optional quality upgrade for longer conversations."
        }
    ];
}
