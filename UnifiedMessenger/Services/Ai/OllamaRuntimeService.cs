using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ollama;

namespace UnifiedMessenger.Services.Ai;

public sealed class OllamaRuntimeService : IDisposable
{
    private static readonly Lazy<OllamaRuntimeService> LazyInstance =
        new(() => new OllamaRuntimeService());

    private readonly IAiInferenceClient _inferenceClient;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly HttpClient? _downloadClient;
    private readonly bool _ownsDownloadClient;
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private readonly SemaphoreSlim _engineGate = new(1, 1);

    private Process? _embeddedProcess;
    private OllamaConnectionState _connectionState = OllamaConnectionState.Unknown;

    internal OllamaRuntimeService(
        IAiInferenceClient? inferenceClient = null,
        Func<AppSettings>? settingsProvider = null,
        HttpClient? downloadClient = null,
        string? runtimeInstallDirectory = null,
        string? embeddedExecutablePath = null,
        string? modelsDirectory = null)
    {
        _inferenceClient = inferenceClient ?? OllamaInferenceClient.Instance;
        _settingsProvider = settingsProvider ?? (() => AppSettingsService.Instance.Settings);
        RuntimeInstallDirectory = runtimeInstallDirectory ?? OllamaOptions.RuntimeInstallDirectory;
        EmbeddedExecutablePath = embeddedExecutablePath ?? OllamaOptions.EmbeddedExecutablePath;
        ModelsDirectory = modelsDirectory ?? OllamaOptions.ModelsDirectory;

        if (downloadClient is null)
        {
            _downloadClient = new HttpClient
            {
                Timeout = OllamaOptions.BootstrapDownloadTimeout
            };
            _ownsDownloadClient = true;
        }
        else
        {
            _downloadClient = downloadClient;
        }
    }

    public static OllamaRuntimeService Instance => LazyInstance.Value;

    public string RuntimeInstallDirectory { get; }

    public string EmbeddedExecutablePath { get; }

    public string ModelsDirectory { get; }

    public bool HasEmbeddedExecutable => File.Exists(EmbeddedExecutablePath);

    public OllamaConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState == value)
            {
                return;
            }

            _connectionState = value;
            ConnectionStateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<OllamaConnectionState>? ConnectionStateChanged;

    public void WarmupInBackground()
    {
        if (!_settingsProvider().EnableLocalAi)
        {
            return;
        }

        _ = WarmupAsync();
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ollama warmup failed: {ex.Message}");
            ConnectionState = OllamaConnectionState.Error;
        }
    }

    public async Task<bool> ProbeSystemOllamaAsync(CancellationToken cancellationToken = default) =>
        await _inferenceClient.TryPingAsync(cancellationToken).ConfigureAwait(false);

    public bool NeedsRuntimeDownload() =>
        !HasEmbeddedExecutable;

    public async Task<bool> DownloadRuntimeAsync(
        IProgress<OllamaRuntimeDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasEmbeddedExecutable)
            {
                progress?.Report(new OllamaRuntimeDownloadProgress
                {
                    Phase = "ready",
                    Completed = 1,
                    Total = 1,
                    IsComplete = true
                });
                return true;
            }

            if (await _inferenceClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                ConnectionState = OllamaConnectionState.Running;
                progress?.Report(new OllamaRuntimeDownloadProgress
                {
                    Phase = "ready",
                    Completed = 1,
                    Total = 1,
                    IsComplete = true
                });
                return true;
            }

            await DownloadAndExtractAsync(progress, cancellationToken).ConfigureAwait(false);
            return HasEmbeddedExecutable;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ollama runtime download failed: {ex.Message}");
            progress?.Report(new OllamaRuntimeDownloadProgress
            {
                Phase = "failed",
                Completed = 0,
                Total = 0,
                IsComplete = false,
                Error = ex.Message
            });
            ConnectionState = OllamaConnectionState.Error;
            return false;
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!_settingsProvider().EnableLocalAi)
        {
            ConnectionState = OllamaConnectionState.NotRunning;
            return false;
        }

        await _engineGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ConnectionState = OllamaConnectionState.Starting;

            if (await _inferenceClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                ConnectionState = OllamaConnectionState.Running;
                return true;
            }

            if (HasEmbeddedExecutable)
            {
                TryStartEmbeddedServer();
                if (await WaitForHealthyAsync(cancellationToken).ConfigureAwait(false))
                {
                    ConnectionState = OllamaConnectionState.Running;
                    return true;
                }
            }

            if (!_settingsProvider().OllamaAutoBootstrap)
            {
                ConnectionState = OllamaConnectionState.NotRunning;
                return false;
            }

            await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (await _inferenceClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
                {
                    ConnectionState = OllamaConnectionState.Running;
                    return true;
                }

                if (!HasEmbeddedExecutable)
                {
                    await DownloadAndExtractAsync(progress: null, cancellationToken).ConfigureAwait(false);
                }

                TryStartEmbeddedServer();
                var healthy = await WaitForHealthyAsync(cancellationToken).ConfigureAwait(false);
                ConnectionState = healthy ? OllamaConnectionState.Running : OllamaConnectionState.NotRunning;
                return healthy;
            }
            finally
            {
                _bootstrapGate.Release();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ollama runtime ensure failed: {ex.Message}");
            ConnectionState = OllamaConnectionState.Error;
            return false;
        }
        finally
        {
            _engineGate.Release();
        }
    }

    internal async Task DownloadAndExtractAsync(
        IProgress<OllamaRuntimeDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RuntimeInstallDirectory);
        Directory.CreateDirectory(ModelsDirectory);

        var zipUri = OllamaOptions.ResolvePinnedWindowsZipUri();
        var zipPath = Path.Combine(OllamaOptions.RuntimeRoot, OllamaOptions.ResolveWindowsZipAssetName());

        progress?.Report(new OllamaRuntimeDownloadProgress
        {
            Phase = "downloading",
            Completed = 0,
            Total = 0,
            IsComplete = false
        });

        await DownloadFileAsync(zipUri, zipPath, progress, cancellationToken).ConfigureAwait(false);
        await VerifySha256Async(zipPath, OllamaOptions.ResolvePinnedWindowsZipSha256(), cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new OllamaRuntimeDownloadProgress
        {
            Phase = "extracting",
            Completed = 0,
            Total = 0,
            IsComplete = false
        });

        var extractRoot = Path.Combine(OllamaOptions.RuntimeRoot, "extract");
        if (Directory.Exists(extractRoot))
        {
            Directory.Delete(extractRoot, recursive: true);
        }

        Directory.CreateDirectory(extractRoot);
        ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true);

        var discoveredExecutable = Directory
            .EnumerateFiles(extractRoot, OllamaOptions.OllamaExecutableName, SearchOption.AllDirectories)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(discoveredExecutable))
        {
            throw new FileNotFoundException(
                $"Ollama bootstrap did not contain {OllamaOptions.OllamaExecutableName}.");
        }

        CopyRuntimeTree(extractRoot, RuntimeInstallDirectory);

        progress?.Report(new OllamaRuntimeDownloadProgress
        {
            Phase = "ready",
            Completed = 1,
            Total = 1,
            IsComplete = true
        });
    }

    private void TryStartEmbeddedServer()
    {
        if (!HasEmbeddedExecutable)
        {
            return;
        }

        if (_embeddedProcess is { HasExited: false })
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(RuntimeInstallDirectory);
            Directory.CreateDirectory(ModelsDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = EmbeddedExecutablePath,
                Arguments = "serve",
                WorkingDirectory = RuntimeInstallDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
            startInfo.Environment["OLLAMA_MODELS"] = ModelsDirectory;
            startInfo.Environment["OLLAMA_NO_CLOUD"] = "true";

            _embeddedProcess = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start embedded Ollama: {ex.Message}");
        }
    }

    private async Task<bool> WaitForHealthyAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _inferenceClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task DownloadFileAsync(
        Uri uri,
        string destinationPath,
        IProgress<OllamaRuntimeDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _downloadClient!
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);

        var buffer = new byte[81920];
        long completed = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            completed += read;
            progress?.Report(new OllamaRuntimeDownloadProgress
            {
                Phase = "downloading",
                Completed = completed,
                Total = totalBytes,
                IsComplete = false
            });
        }
    }

    private static async Task VerifySha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Ollama runtime checksum mismatch: expected {expectedSha256}, got {actual}");
        }
    }

    private static void CopyRuntimeTree(string sourceRoot, string destinationRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var target = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    public void Shutdown()
    {
        try
        {
            if (_embeddedProcess is { HasExited: false })
            {
                _embeddedProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop embedded Ollama: {ex.Message}");
        }
        finally
        {
            _embeddedProcess?.Dispose();
            _embeddedProcess = null;
            ConnectionState = OllamaConnectionState.NotRunning;
        }
    }

    public void Dispose()
    {
        Shutdown();
        _bootstrapGate.Dispose();
        _engineGate.Dispose();

        if (_ownsDownloadClient)
        {
            _downloadClient?.Dispose();
        }
    }
}
