namespace UnifiedMessenger.Services;

/// <summary>
/// Wave 1 security remediation tracker for re-audit validation (S1–S10).
/// </summary>
public static class SecurityAuditChecklist
{
    public const string VulnerablePackageScanCommand =
        "dotnet list package --vulnerable --include-transitive";

    public static IReadOnlyList<SecurityAuditItem> Items { get; } =
    [
        new("S1", "PrepareScript JSON-encodes instance identifiers", "PlatformAdapters.cs", true),
        new("S2", "Import validates StartUrl", "InstanceRegistryService.cs", true),
        new("S3", "Auto-update verifies SHA-256 sidecar", "GitHubUpdateService.cs", true),
        new("S4", "PromptBeforeAutoUpdate honored", "GitHubUpdateService.cs", true),
        new("S5", "ARM64 installer path documented in CI", "build.yml", true),
        new("S6", "Operational clear includes triage, threads, analytics, alerts", "OperationalDataService.cs", true),
        new("S7", "Clear notifications resets badges", "NotificationHub.cs", true),
        new("S8", "LLM customer message fencing", "AiDraftPromptService.cs", true),
        new("S9", "Typed WebView script gateway", "IWebViewScriptGateway.cs", true),
        new("S10", "Review selector uses CSS.escape fallback", "adapter-core.js", true)
    ];

    public static int ResolvedHighSeverityCount => Items.Count(item => item.IsResolved);

    public sealed record SecurityAuditItem(
        string Id,
        string Description,
        string PrimaryFile,
        bool IsResolved);
}
