using System.Net.Http;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

/// <summary>
/// Installs an updated selector manifest from the release channel, so a client redesign is a data fix
/// rather than a new binary for every customer.
/// </summary>
/// <remarks>
/// <para><b>Why this is allowed to exist at all.</b> The same GitHub release the updater already reads is
/// where the installer itself comes from. Anyone who could serve a malicious manifest here could serve a
/// malicious <i>executable</i> on the identical channel, which is strictly worse — so this adds no new
/// trust, no new host, and no new failure mode to the ones the product already accepts. What it must not
/// do is quietly become something more than that, and the rules below are what hold it there.</para>
/// <list type="number">
/// <item><b>It is data, never code.</b> A manifest is CSS selector strings, colour values and booleans.
/// There is no field that can carry a URL, a script, or a path, and <see cref="Validate"/> rejects a file
/// containing markup or a <c>javascript:</c> scheme rather than trusting that absence.</item>
/// <item><b>Nothing goes out.</b> One HTTPS GET to an asset URL that came from the release payload, with
/// the updater's existing constant <c>User-Agent</c> and no query, no body, no header built from anything
/// the app knows. There is no request shape here that could carry a customer's data even by accident.</item>
/// <item><b>A bad manifest cannot break the scraper.</b> It is validated <i>before</i> it is written, so a
/// truncated download or a file aimed at the wrong platform never reaches disk. Even if one did,
/// <see cref="SelectorManifestLoader"/> falls back to the compiled-in default and then to the selector at
/// the JS call site.</item>
/// <item><b>It rides the user's existing choice.</b> It runs inside the update check, so an owner who has
/// turned auto-update off gets no new outbound request they did not ask for.</item>
/// </list>
/// <para>Recorded in <c>docs/egress-inventory.md</c> §1. If the request shape here ever changes, that file
/// changes with it.</para>
/// </remarks>
public static class SelectorManifestUpdater
{
    /// <summary>Release assets named like this carry a manifest, one per platform.</summary>
    public const string AssetPrefix = "selectors-";
    public const string AssetSuffix = ".json";

    /// <summary>
    /// A manifest is a few kilobytes. The cap is not about disk — it stops a hostile or corrupted asset
    /// being read into memory in full before anything has had a chance to reject it.
    /// </summary>
    public const int MaxManifestBytes = 256 * 1024;

    // Shape limits. A real manifest is nowhere near any of these; they exist so that a file which is
    // structurally valid JSON but absurd is refused at the boundary rather than absorbed.
    private const int MaxAnchors = 128;
    private const int MaxCandidatesPerAnchor = 16;
    private const int MaxCandidateLength = 512;
    private const int MaxTextFieldLength = 4096;

    /// <summary>
    /// Hosts an asset may be fetched from. The installer path checks only that a URL is HTTPS; that is
    /// looser than it should be, and this deliberately does not inherit the gap — a new outbound path
    /// should not widen the old one by copying it.
    /// </summary>
    private static readonly string[] TrustedHosts =
    [
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "raw.githubusercontent.com",
        "release-assets.githubusercontent.com"
    ];

    public static bool IsTrustedAssetUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && TrustedHosts.Any(h =>
            uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

    /// <summary>The platform id an asset name refers to, or null when the name is not a manifest asset.</summary>
    public static string? PlatformFromAssetName(string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        var name = assetName.Trim();
        if (!name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = name[AssetPrefix.Length..^AssetSuffix.Length];
        if (id.Length == 0)
        {
            return null;
        }

        // Only a platform this build actually knows. An asset for a platform we have never heard of is
        // not a manifest we can validate, so it is not one we should write.
        //
        // Returns the CANONICAL id, not the slice off the asset name. The lookup is case-insensitive, so
        // `SELECTORS-WHATSAPP.JSON` resolves — but handing back "WHATSAPP" would then be used to build the
        // override path and to match the manifest's own `platform` field, and neither is case-folded the
        // same way. Normalising here is what keeps a differently-cased asset name from writing a second,
        // shadow manifest file the loader never reads.
        return PlatformDefinition.FindById(id)?.Id;
    }

    /// <summary>
    /// Checks a downloaded manifest before it is allowed anywhere near disk. Pure, so the whole decision
    /// table is testable without a network or a file system.
    /// </summary>
    public static bool Validate(string? json, string expectedPlatform, out SelectorManifest? manifest, out string reason)
    {
        manifest = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            reason = "empty";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxManifestBytes)
        {
            reason = "too large";
            return false;
        }

        // The same parse the loader uses, so a file that passes here is a file the loader will accept:
        // schema version, platform match, and no anchor with an empty candidate list.
        var parsed = SelectorManifestLoader.Resolve(expectedPlatform, json, null);
        if (parsed is null)
        {
            reason = "rejected by the manifest parser (schema version, platform, or an empty anchor)";
            return false;
        }

        if (parsed.Anchors.Count > MaxAnchors)
        {
            reason = $"{parsed.Anchors.Count} anchors exceeds the {MaxAnchors} limit";
            return false;
        }

        if (parsed.ObservedAgainst.Length > MaxTextFieldLength)
        {
            reason = "a text field is over length";
            return false;
        }

        foreach (var (name, anchor) in parsed.Anchors)
        {
            if (anchor.Candidates.Count > MaxCandidatesPerAnchor)
            {
                reason = $"anchor '{name}' has {anchor.Candidates.Count} candidates";
                return false;
            }

            foreach (var candidate in anchor.Candidates)
            {
                if (candidate.Length > MaxCandidateLength)
                {
                    reason = $"anchor '{name}' has an over-length selector";
                    return false;
                }

                // A CSS selector contains none of these. Their presence means the file is not what it
                // claims to be, and the right response to that is to refuse it, not to reason about
                // whether this particular one could do harm.
                if (candidate.Contains('<')
                    || candidate.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains("expression(", StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"anchor '{name}' contains a selector that is not a selector";
                    return false;
                }
            }
        }

        manifest = parsed;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Downloads, validates and installs every manifest asset on the release. Returns how many were
    /// installed. Never throws into the update check.
    /// </summary>
    public static async Task<int> TryApplyAsync(
        IReadOnlyDictionary<string, string> manifestAssets,
        Func<string, CancellationToken, Task<string?>> download,
        CancellationToken cancellationToken)
    {
        if (manifestAssets.Count == 0)
        {
            return 0;
        }

        var installed = 0;
        foreach (var (assetName, url) in manifestAssets)
        {
            var platform = PlatformFromAssetName(assetName);
            if (platform is null)
            {
                continue;
            }

            if (!IsTrustedAssetUrl(url))
            {
                AppLogger.LogWarning("Selectors", $"Ignoring '{assetName}': the asset URL is not on a trusted host.");
                continue;
            }

            try
            {
                var json = await download(url, cancellationToken).ConfigureAwait(false);
                if (!Validate(json, platform, out var manifest, out var reason))
                {
                    AppLogger.LogWarning("Selectors", $"Ignoring '{assetName}': {reason}.");
                    continue;
                }

                var path = SelectorManifestLoader.OverridePath(platform);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                // Write beside, then move over: a process killed mid-write must not leave a half file
                // where the loader expects a manifest.
                var temp = path + ".tmp";
                await File.WriteAllTextAsync(temp, json!, cancellationToken).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);

                SelectorManifestLoader.ResetCache();
                installed++;
                AppLogger.LogInfo(
                    "Selectors",
                    $"Installed an updated manifest for '{platform}' (schema {manifest!.SchemaVersion}, observed against {manifest.ObservedAgainst}).");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException or UnauthorizedAccessException)
            {
                AppLogger.LogWarning("Selectors", $"Could not install '{assetName}': {ex.GetType().Name}.");
            }
        }

        return installed;
    }
}
