using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
namespace UnifiedMessenger.Services.Ollama;

internal sealed class OllamaBootstrapService : IDisposable
{
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private readonly HttpClient _downloadClient;
    private readonly bool _ownsDownloadClient;
    private Process? _embeddedProcess;

    public OllamaBootstrapService(HttpClient? downloadClient = null)
    {
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

    public bool HasEmbeddedExecutable =>
        File.Exists(OllamaOptions.EmbeddedExecutablePath);

    public async Task<bool> EnsureRunningAsync(
        OllamaHttpClient apiClient,
        bool allowDownload,
        CancellationToken cancellationToken = default)
    {
        if (await apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (HasEmbeddedExecutable)
        {
            TryStartEmbeddedServer();
            await WaitForHealthyAsync(apiClient, cancellationToken).ConfigureAwait(false);
            return await apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!allowDownload)
        {
            return false;
        }

        await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await DownloadAndExtractAsync(cancellationToken).ConfigureAwait(false);
            TryStartEmbeddedServer();
            await WaitForHealthyAsync(apiClient, cancellationToken).ConfigureAwait(false);
            return await apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    internal async Task DownloadAndExtractAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(OllamaOptions.RuntimeInstallDirectory);

        var zipUri = OllamaOptions.ResolveLatestWindowsZipUri();
        var zipPath = Path.Combine(OllamaOptions.RuntimeRoot, OllamaOptions.ResolveWindowsZipAssetName());

        await DownloadFileAsync(zipUri, zipPath, cancellationToken).ConfigureAwait(false);

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

        CopyRuntimeTree(extractRoot, OllamaOptions.RuntimeInstallDirectory);
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
            var startInfo = new ProcessStartInfo
            {
                FileName = OllamaOptions.EmbeddedExecutablePath,
                Arguments = "serve",
                WorkingDirectory = OllamaOptions.RuntimeInstallDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _embeddedProcess = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start embedded Ollama: {ex.Message}");
        }
    }

    private static async Task WaitForHealthyAsync(
        OllamaHttpClient apiClient,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await apiClient.TryPingAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DownloadFileAsync(Uri uri, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _downloadClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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

    public void Dispose()
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
            if (_ownsDownloadClient)
            {
                _downloadClient.Dispose();
            }

            _bootstrapGate.Dispose();
        }
    }
}
