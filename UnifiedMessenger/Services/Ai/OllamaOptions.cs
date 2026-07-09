namespace UnifiedMessenger.Services.Ai;

internal static class OllamaOptions
{
    public const string DefaultEndpoint = "http://127.0.0.1:11434/";

    public const string DefaultModelName = "phi3:mini";

    public const string PinnedReleaseVersion = "v0.30.8";

    public const string RuntimeFolderName = "ollama";

    public const string OllamaExecutableName = "ollama.exe";

    public const int DefaultTopNUrgentThreads = 5;

    public const int QueueCapacity = 32;

    public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan InferenceTimeout = TimeSpan.FromSeconds(45);

    public static readonly TimeSpan BootstrapDownloadTimeout = TimeSpan.FromMinutes(30);

    public static string RuntimeRoot =>
        Path.Combine(ApplicationPaths.UserDataRoot, RuntimeFolderName);

    public static string RuntimeInstallDirectory => Path.Combine(RuntimeRoot, "runtime");

    public static string ModelsDirectory => Path.Combine(RuntimeRoot, "models");

    public static string EmbeddedExecutablePath =>
        Path.Combine(RuntimeInstallDirectory, OllamaExecutableName);

    internal static string ResolveWindowsZipAssetName() =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "ollama-windows-arm64.zip",
            _ => "ollama-windows-amd64.zip"
        };

    internal static Uri ResolvePinnedWindowsZipUri() =>
        new($"https://github.com/ollama/ollama/releases/download/{PinnedReleaseVersion}/{ResolveWindowsZipAssetName()}");

    internal static string ResolvePinnedWindowsZipSha256() =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 =>
                "487fa170d6eedc3ce12fbf144a39970d8322c4c6efbaa9a366ad7aa8769f5713",
            _ => "c2d26d97e698027329c252629d7113bbc05d874b49960cbb03e93a39ae9fd95c"
        };

    internal static string NormalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return DefaultEndpoint;
        }

        var trimmed = endpoint.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
    }
}
