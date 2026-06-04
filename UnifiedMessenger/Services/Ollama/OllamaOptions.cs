namespace UnifiedMessenger.Services.Ollama;

internal static class OllamaOptions
{
    public const string DefaultBaseUrl = "http://127.0.0.1:11434/";

    public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan GenerateTimeout = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan BootstrapDownloadTimeout = TimeSpan.FromMinutes(30);

    public const string DefaultModelName = "phi3:mini";

    public const string RuntimeFolderName = "ollama";

    public const string OllamaExecutableName = "ollama.exe";

    public static string RuntimeRoot =>
        Path.Combine(ApplicationPaths.UserDataRoot, RuntimeFolderName);

    public static string RuntimeInstallDirectory => Path.Combine(RuntimeRoot, "runtime");

    public static string EmbeddedExecutablePath =>
        Path.Combine(RuntimeInstallDirectory, OllamaExecutableName);

    internal static string ResolveWindowsZipAssetName() =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "ollama-windows-arm64.zip",
            _ => "ollama-windows-amd64.zip"
        };

    internal static Uri ResolveLatestWindowsZipUri() =>
        new($"https://github.com/ollama/ollama/releases/latest/download/{ResolveWindowsZipAssetName()}");
}
