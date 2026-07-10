using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace UnifiedMessenger.Services;

internal static class InstallerIntegrityVerifier
{
    /// <summary>
    /// Optional publisher pin. When non-empty, the downloaded installer's Authenticode signer
    /// certificate subject must contain this string (case-insensitive) in addition to chaining to
    /// a trusted root. Leave empty to accept any validly-trusted Authenticode signature.
    /// Set this to the production code-signing certificate subject CN once signing is in place,
    /// e.g. "CN=Anfal Haider".
    /// </summary>
    internal const string ExpectedPublisherSubstring = "";

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

        // Authenticode policy validation: verifies the embedded signature is intact AND the signing
        // certificate chains to a trusted root (WinVerifyTrust), not merely that a signature exists.
        if (!TryVerifyAuthenticodeTrust(installerPath, out var trustError))
        {
            errorMessage = trustError;
            return false;
        }

        // Optional publisher pinning — defends against a validly-signed but unexpected publisher.
        if (!string.IsNullOrEmpty(ExpectedPublisherSubstring) &&
            !TryVerifyPublisher(installerPath, ExpectedPublisherSubstring, out var publisherError))
        {
            errorMessage = publisherError;
            return false;
        }

        return true;
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

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool TryVerifyAuthenticodeTrust(string filePath, out string? errorMessage)
    {
        errorMessage = null;

        var fileInfo = new WinTrustFileInfo
        {
            cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            pcwszFilePath = filePath,
            hFile = IntPtr.Zero,
            pgKnownSubject = IntPtr.Zero
        };

        var pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, pFileInfo, false);

            var data = new WinTrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_WHOLECHAIN,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFileInfo,
                dwStateAction = WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = WTD_REVOCATION_CHECK_CHAIN,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero
            };

            var actionGuid = WintrustActionGenericVerifyV2;
            var result = WinVerifyTrust(IntPtr.Zero, ref actionGuid, ref data);

            // Always release the state data regardless of verify result.
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            _ = WinVerifyTrust(IntPtr.Zero, ref actionGuid, ref data);

            if (result == 0)
            {
                return true;
            }

            errorMessage = (uint)result switch
            {
                TRUST_E_NOSIGNATURE => "Downloaded installer is not Authenticode-signed.",
                TRUST_E_BAD_DIGEST => "Downloaded installer signature does not match its contents (tampered).",
                TRUST_E_SUBJECT_NOT_TRUSTED => "Downloaded installer signature is not trusted.",
                CERT_E_UNTRUSTEDROOT => "Downloaded installer certificate does not chain to a trusted root.",
                CERT_E_EXPIRED => "Downloaded installer signing certificate has expired.",
                CERT_E_REVOKED => "Downloaded installer signing certificate was revoked.",
                _ => $"Downloaded installer failed Authenticode trust verification (0x{result:X8})."
            };
            return false;
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(pFileInfo);
            Marshal.FreeHGlobal(pFileInfo);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool TryVerifyPublisher(string filePath, string expectedSubstring, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            if (cert.Subject.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            errorMessage = "Downloaded installer was signed by an unexpected publisher.";
            return false;
        }
        catch (CryptographicException ex)
        {
            errorMessage = $"Could not read installer publisher certificate: {ex.Message}";
            return false;
        }
    }

    // WinVerifyTrust interop ------------------------------------------------------------------

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_WHOLECHAIN = 1;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_REVOCATION_CHECK_CHAIN = 0x00000040;

    private const uint TRUST_E_NOSIGNATURE = 0x800B0100;
    private const uint TRUST_E_BAD_DIGEST = 0x80096010;
    private const uint TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004;
    private const uint CERT_E_UNTRUSTEDROOT = 0x800B0109;
    private const uint CERT_E_EXPIRED = 0x800B0101;
    private const uint CERT_E_REVOKED = 0x800B010C;

    private static Guid WintrustActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WinTrustData pWVTData);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }
}
