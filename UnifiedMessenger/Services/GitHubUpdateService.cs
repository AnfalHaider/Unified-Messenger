using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace UnifiedMessenger.Services;

/// <summary>
/// Checks GitHub Releases for a newer installer and applies it silently via Inno Setup.
/// </summary>
public sealed class GitHubUpdateService
{
    private const string GitHubOwner = "AnfalHaider";
    private const string GitHubRepo = "Unified-Messenger";
    private const string SetupAssetName = "UnifiedMessengerSetup.exe";
    private const string UserAgent = "UnifiedMessenger-Updater/1.0";

    private static readonly Lazy<GitHubUpdateService> LazyInstance = new(() => new GitHubUpdateService());

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static GitHubUpdateService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public static GitHubUpdateService Instance => LazyInstance.Value;

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var releaseUrl =
                $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

            using var response = await HttpClient
                .GetAsync(releaseUrl, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);

            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                return;
            }

            var tagName = tagElement.GetString();
            if (!TryParseReleaseVersion(tagName, out var latestVersion))
            {
                return;
            }

            var currentVersion = GetCurrentVersion();
            if (latestVersion <= currentVersion)
            {
                return;
            }

            if (!root.TryGetProperty("assets", out var assetsElement) ||
                assetsElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            string? downloadUrl = null;
            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                if (!SetupAssetName.Equals(nameElement.GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    downloadUrl = urlElement.GetString();
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return;
            }

            var installerPath = Path.Combine(
                Path.GetTempPath(),
                $"UnifiedMessengerSetup_{latestVersion}.exe");

            await DownloadFileAsync(downloadUrl, installerPath, timeoutCts.Token).ConfigureAwait(false);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true
            });

            if (App.CurrentWindow?.DispatcherQueue is { } dispatcher)
            {
                dispatcher.TryEnqueue(() => Application.Current.Exit());
            }
            else
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected update check failure: {ex.Message}");
        }
    }

    private static async Task DownloadFileAsync(
        string downloadUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var responseStream = await HttpClient
            .GetStreamAsync(downloadUrl, cancellationToken)
            .ConfigureAwait(false);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private static bool TryParseReleaseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var normalized = tagName.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        if (!Version.TryParse(normalized, out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
