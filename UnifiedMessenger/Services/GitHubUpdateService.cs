using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Checks GitHub Releases for a newer installer and applies it silently via Inno Setup.
/// </summary>
public sealed class GitHubUpdateService
{
    internal const string DefaultGitHubOwner = "AnfalHaider";

    internal const string DefaultGitHubRepo = "Unified-Messenger";

    internal const string SetupAssetName = "UnifiedMessengerSetup.exe";

    internal const string UserAgent = "UnifiedMessenger-Updater/1.0";

    internal const string GitHubTokenEnvironmentVariable = "UNIFIED_MESSENGER_GITHUB_TOKEN";

    private static readonly Lazy<GitHubUpdateService> LazyInstance = new(() => new GitHubUpdateService());

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static GitHubUpdateService Instance => LazyInstance.Value;

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!AppSettingsService.Instance.Settings.EnableAutoUpdate)
        {
            return;
        }

        var result = await CheckForUpdatesInternalAsync(cancellationToken).ConfigureAwait(false);
        if (result.Status != UpdateCheckStatus.UpdateAvailable ||
            string.IsNullOrWhiteSpace(result.DownloadUrl) ||
            result.LatestVersion is null)
        {
            return;
        }

        if (AppSettingsService.Instance.Settings.PromptBeforeAutoUpdate)
        {
            return;
        }

        await ApplyUpdateAsync(result.DownloadUrl, result.LatestVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesManualAsync(CancellationToken cancellationToken = default)
    {
        return await CheckForUpdatesInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<UpdateCheckResult> CheckForUpdatesInternalAsync(CancellationToken cancellationToken)
    {
        var currentVersion = GetCurrentVersion();
        var owner = DefaultGitHubOwner;
        var repo = DefaultGitHubRepo;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var release = await TryFetchLatestReleaseAsync(owner, repo, timeoutCts.Token)
                .ConfigureAwait(false);

            if (release is null)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Failed,
                    currentVersion,
                    ErrorMessage: DescribeUnavailableReleaseSource(owner, repo));
            }

            if (!TryParseReleaseVersion(release.TagName, out var latestVersion))
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Failed,
                    currentVersion,
                    ErrorMessage: "Could not parse the latest release version.");
            }

            if (!IsNewerVersion(currentVersion, latestVersion))
            {
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, currentVersion, latestVersion);
            }

            if (string.IsNullOrWhiteSpace(release.DownloadUrl))
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Failed,
                    currentVersion,
                    latestVersion,
                    ErrorMessage: $"Release {release.TagName} is missing installer asset '{SetupAssetName}'.");
            }

            if (!IsValidDownloadUrl(release.DownloadUrl))
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Failed,
                    currentVersion,
                    latestVersion,
                    ErrorMessage: "The update download URL is invalid.");
            }

            return new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                currentVersion,
                latestVersion,
                DownloadUrl: release.DownloadUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                currentVersion,
                ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected update check failure: {ex.Message}");
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                currentVersion,
                ErrorMessage: ex.Message);
        }
    }

    private static async Task<GitHubReleaseInfo?> TryFetchLatestReleaseAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var latestUrl = BuildLatestReleaseUrl(owner, repo);
        using var latestResponse = await HttpClient.GetAsync(latestUrl, cancellationToken).ConfigureAwait(false);

        if (latestResponse.IsSuccessStatusCode)
        {
            await using var stream = await latestResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ParseRelease(document.RootElement);
        }

        if (latestResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            latestResponse.EnsureSuccessStatusCode();
        }

        if (!await RepositoryExistsAsync(owner, repo, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var releasesUrl = BuildReleasesListUrl(owner, repo);
        using var releasesResponse = await HttpClient.GetAsync(releasesUrl, cancellationToken).ConfigureAwait(false);
        releasesResponse.EnsureSuccessStatusCode();

        await using var releasesStream = await releasesResponse.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var releasesDocument = await JsonDocument
            .ParseAsync(releasesStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return SelectFirstPublishedRelease(releasesDocument.RootElement);
    }

    private static async Task<bool> RepositoryExistsAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var repoUrl = BuildRepositoryUrl(owner, repo);
        using var response = await HttpClient.GetAsync(repoUrl, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    internal static string BuildLatestReleaseUrl(string owner, string repo) =>
        $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

    internal static string BuildReleasesListUrl(string owner, string repo, int pageSize = 5) =>
        $"https://api.github.com/repos/{owner}/{repo}/releases?per_page={pageSize}";

    internal static string BuildRepositoryUrl(string owner, string repo) =>
        $"https://api.github.com/repos/{owner}/{repo}";

    internal static GitHubReleaseInfo? SelectFirstPublishedRelease(JsonElement releasesElement)
    {
        if (releasesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var releaseElement in releasesElement.EnumerateArray())
        {
            if (releaseElement.TryGetProperty("draft", out var draftElement) &&
                draftElement.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            var release = ParseRelease(releaseElement);
            if (release is not null && TryParseReleaseVersion(release.TagName, out _))
            {
                return release;
            }
        }

        return null;
    }

    internal static GitHubReleaseInfo? ParseRelease(JsonElement root)
    {
        if (!root.TryGetProperty("tag_name", out var tagElement))
        {
            return null;
        }

        var tagName = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assetsElement) &&
            assetsElement.ValueKind == JsonValueKind.Array)
        {
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
        }

        return new GitHubReleaseInfo(tagName.Trim(), downloadUrl);
    }

    internal static string DescribeUnavailableReleaseSource(
        string owner,
        string repo,
        bool? tokenConfiguredOverride = null)
    {
        var repoUrl = $"https://github.com/{owner}/{repo}";
        var tokenConfigured = tokenConfiguredOverride
            ?? !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(GitHubTokenEnvironmentVariable));

        if (tokenConfigured)
        {
            return
                $"No accessible release was found for {owner}/{repo}. " +
                $"Publish a GitHub release with asset '{SetupAssetName}', or verify the token in {GitHubTokenEnvironmentVariable}.";
        }

        return
            $"Updates are unavailable because {repoUrl} is not public, does not exist yet, or has no published releases. " +
            $"Create a release on GitHub and attach '{SetupAssetName}' to enable update checks.";
    }

    internal static bool IsNewerVersion(Version currentVersion, Version latestVersion) =>
        latestVersion > currentVersion;

    internal static bool IsValidDownloadUrl(string? downloadUrl) =>
        Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var token = Environment.GetEnvironmentVariable(GitHubTokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        return client;
    }

    internal sealed record GitHubReleaseInfo(string TagName, string? DownloadUrl);

    private static async Task ApplyUpdateAsync(
        string downloadUrl,
        Version latestVersion,
        CancellationToken cancellationToken)
    {
        if (!IsValidDownloadUrl(downloadUrl))
        {
            throw new InvalidOperationException("Update download URL must be HTTPS.");
        }

        var installerPath = Path.Combine(
            Path.GetTempPath(),
            $"UnifiedMessengerSetup_{latestVersion}.exe");

        await DownloadFileAsync(downloadUrl, installerPath, cancellationToken).ConfigureAwait(false);

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });

        if (App.CurrentWindow?.DispatcherQueue is { } dispatcher)
        {
            dispatcher.TryEnqueue(Application.Current.Exit);
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private static async Task DownloadFileAsync(
        string downloadUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
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

    internal static bool TryParseReleaseVersion(string? tagName, out Version version)
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
