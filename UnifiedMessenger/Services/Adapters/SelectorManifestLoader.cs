using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Services.Adapters;

/// <summary>
/// Loads a platform's <see cref="SelectorManifest"/>, preferring a user-data override over the
/// compiled-in default so a client redesign can be fixed without shipping a binary to every customer.
/// </summary>
/// <remarks>
/// <para><b>The load can degrade, but it can never produce a dead scraper.</b> There are two sources and
/// three outcomes:</para>
/// <list type="number">
/// <item>An override at <c>%LOCALAPPDATA%\UnifiedMessenger\selectors\&lt;platform&gt;.json</c> — absent
/// today; the increment that delivers one over the existing update channel writes it here.</item>
/// <item>The compiled-in default, embedded in the assembly. It cannot be deleted by a partial install,
/// corrupted on disk, or locked by a scanner — which is why it is an embedded resource rather than a
/// <c>Content</c> file beside the scripts.</item>
/// <item>Neither parses: the injection emits nothing, and every JS call site falls back to the selector
/// compiled into the script. That is the floor, and it is the behaviour the app has today.</item>
/// </list>
/// <para>An override that is missing, unreadable, malformed, aimed at another platform, or written
/// against a schema version this build does not know is <b>ignored with a logged warning</b>, never
/// thrown. The whole point of the override is that it arrives from outside the build; treating a bad one
/// as fatal would hand a remote file the power to stop the scraper.</para>
/// </remarks>
public static class SelectorManifestLoader
{
    /// <summary>The schema this build understands. A manifest declaring anything else is ignored.</summary>
    public const int SupportedSchemaVersion = 1;

    private const string EmbeddedPrefix = "UnifiedMessenger.Assets.Config.selectors.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly ConcurrentDictionary<string, SelectorManifest?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The manifest a platform reads. WhatsApp and WhatsApp Business share one adapter, so they share one
    /// manifest — the same collapse <see cref="PlatformDefinition"/> already makes for their capabilities.
    /// </summary>
    internal static string ManifestIdFor(string? platformId) =>
        PlatformDefinition.NormalizePlatformId(platformId) switch
        {
            "whatsappbusiness" => "whatsapp",
            var id => id
        };

    /// <summary>Where an updatable override for <paramref name="platformId"/> would live.</summary>
    public static string OverridePath(string platformId) => Path.Combine(
        ApplicationPaths.UserDataRoot,
        "selectors",
        ManifestIdFor(platformId) + ".json");

    /// <summary>
    /// The manifest for <paramref name="platformId"/>, or null when this platform has none yet. Cached:
    /// manifests are read once per process, like the adapter scripts beside them.
    /// </summary>
    public static SelectorManifest? ForPlatform(string platformId)
    {
        var id = ManifestIdFor(platformId);
        return Cache.GetOrAdd(id, static key => Load(key));
    }

    /// <summary>Drops the cache. For tests, and for a future "reload manifests" action.</summary>
    public static void ResetCache() => Cache.Clear();

    private static SelectorManifest? Load(string platformId)
    {
        var embedded = ReadEmbedded(platformId);
        string? overrideJson = null;

        try
        {
            var path = OverridePath(platformId);
            if (File.Exists(path))
            {
                overrideJson = File.ReadAllText(path);
            }
        }
        catch (Exception ex)
        {
            // Locked by a scanner, unreachable folder, denied - all the same answer: use the default.
            AppLogger.LogWarning(
                "Selectors",
                $"Could not read the selector override for '{platformId}': {ex.GetType().Name}. Using the built-in manifest.");
        }

        return Resolve(platformId, overrideJson, embedded);
    }

    /// <summary>
    /// Picks between an override and the compiled-in default. Pure: no file access, no statics, so the
    /// decision table is testable without touching the user's real data directory.
    /// </summary>
    internal static SelectorManifest? Resolve(string platformId, string? overrideJson, string? embeddedJson)
    {
        if (!string.IsNullOrWhiteSpace(overrideJson))
        {
            var candidate = TryParse(overrideJson, platformId, "override");
            if (candidate is not null)
            {
                AppLogger.LogInfo(
                    "Selectors",
                    $"Using the selector override for '{platformId}' (schema {candidate.SchemaVersion}, observed against {candidate.ObservedAgainst}).");
                return candidate;
            }
        }

        return TryParse(embeddedJson, platformId, "built-in");
    }

    private static SelectorManifest? TryParse(string? json, string platformId, string source)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        SelectorManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SelectorManifest>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            AppLogger.LogWarning("Selectors", $"The {source} selector manifest for '{platformId}' is malformed: {ex.Message}");
            return null;
        }

        if (manifest is null)
        {
            return null;
        }

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            AppLogger.LogWarning(
                "Selectors",
                $"The {source} selector manifest for '{platformId}' declares schema {manifest.SchemaVersion}; this build understands {SupportedSchemaVersion}. Ignoring it.");
            return null;
        }

        if (!string.Equals(manifest.Platform, platformId, StringComparison.OrdinalIgnoreCase))
        {
            AppLogger.LogWarning(
                "Selectors",
                $"The {source} selector manifest at '{platformId}' declares platform '{manifest.Platform}'. Ignoring it.");
            return null;
        }

        // An anchor with no candidates would resolve to nothing while looking configured, which is worse
        // than being absent: the JS fallback only engages for an anchor the manifest does not answer.
        if (manifest.Anchors.Any(a => a.Value.Candidates.Count == 0))
        {
            AppLogger.LogWarning(
                "Selectors",
                $"The {source} selector manifest for '{platformId}' has an anchor with no candidates. Ignoring it.");
            return null;
        }

        return manifest;
    }

    private static string? ReadEmbedded(string platformId)
    {
        var name = EmbeddedPrefix + platformId + ".json";
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Selectors", $"Could not read the built-in selector manifest for '{platformId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The document-created script that hands the manifest to the page, or an empty string when this
    /// platform has none. Injected before the adapter scripts so <c>window.__umPick</c> can see it.
    /// </summary>
    public static string BuildInjectionScript(string platformId)
    {
        var manifest = ForPlatform(platformId);
        if (manifest is null)
        {
            return string.Empty;
        }

        return "window.__umSelectors = " + JsonSerializer.Serialize(manifest, JsonOptions) + ";";
    }
}
