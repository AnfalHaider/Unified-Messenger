using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace UnifiedMessenger.Services;

internal static class InstallerIntegrityVerifier
{
    public static bool TryVerifyDownloadedInstaller(
        string installerPath,
        string? expectedSha256,
        out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            errorMessage = "Downloaded installer file was not found.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actualHash = ComputeSha256Hex(installerPath);
            if (!actualHash.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Downloaded installer failed SHA-256 verification.";
                return false;
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            _ = X509Certificate.CreateFromSignedFile(installerPath);
            return true;
        }
        catch (CryptographicException ex)
        {
            errorMessage = $"Downloaded installer is not Authenticode-signed: {ex.Message}";
            return false;
        }
    }

    internal static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    internal static bool TryParseSha256Sidecar(string? sidecarText, out string? sha256Hex)
    {
        sha256Hex = null;
        if (string.IsNullOrWhiteSpace(sidecarText))
        {
            return false;
        }

        var line = sidecarText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.Length > 0);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var token = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
        {
            return false;
        }

        sha256Hex = token;
        return true;
    }
}
