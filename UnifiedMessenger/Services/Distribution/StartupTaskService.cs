using Microsoft.Win32;

namespace UnifiedMessenger.Services;

public static class StartupTaskService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    internal const string ValueName = "UnifiedMessenger";

    public static bool IsRegistered()
    {
        return TryGetRegisteredRunValue(out _);
    }

    public static bool IsRegisteredForCurrentExecutable()
    {
        if (!TryGetRegisteredRunValue(out var registeredValue) ||
            !TryParseRunValue(registeredValue, out var registeredPath))
        {
            return false;
        }

        var currentPath = ResolveStartupExecutablePath();
        return currentPath is not null && PathsEqual(registeredPath, currentPath);
    }

    public static void SetRegistered(bool enabled, string? executablePath = null)
    {
        using var key = OpenRunKey(writable: true)
            ?? throw new InvalidOperationException("Could not open startup registry key.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = executablePath ?? ResolveStartupExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("Could not resolve application executable path.");
        }

        key.SetValue(ValueName, BuildRunValue(exePath));
    }

    public static bool EnsureRegistrationMatchesPreference(bool launchAtStartup)
    {
        var currentPath = ResolveStartupExecutablePath();
        TryGetRegisteredRunValue(out var registeredValue);
        var action = ResolveRegistrationAction(launchAtStartup, registeredValue, currentPath);

        switch (action)
        {
            case StartupRegistrationAction.Register when currentPath is not null:
                SetRegistered(true, currentPath);
                return true;
            case StartupRegistrationAction.Unregister:
                SetRegistered(false);
                return false;
            default:
                return launchAtStartup && IsRegisteredForCurrentExecutable();
        }
    }

    internal static string BuildRunValue(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\"";
    }

    internal static bool TryParseRunValue(string? value, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            trimmed = trimmed[1..^1];
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        executablePath = trimmed;
        return true;
    }

    internal static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    internal static string? ResolveStartupExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(processPath) ? null : processPath;
    }

    internal static StartupRegistrationAction ResolveRegistrationAction(
        bool launchAtStartup,
        string? registeredRunValue,
        string? currentExecutablePath)
    {
        var hasRegistration = TryParseRunValue(registeredRunValue, out var registeredPath);
        var matchesCurrentExecutable = hasRegistration
            && currentExecutablePath is not null
            && PathsEqual(registeredPath, currentExecutablePath);

        if (launchAtStartup)
        {
            if (currentExecutablePath is null)
            {
                return StartupRegistrationAction.None;
            }

            return matchesCurrentExecutable
                ? StartupRegistrationAction.None
                : StartupRegistrationAction.Register;
        }

        return hasRegistration
            ? StartupRegistrationAction.Unregister
            : StartupRegistrationAction.None;
    }

    private static bool TryGetRegisteredRunValue(out string? runValue)
    {
        using var key = OpenRunKey(writable: false);
        runValue = key?.GetValue(ValueName)?.ToString();
        return !string.IsNullOrWhiteSpace(runValue);
    }

    private static RegistryKey? OpenRunKey(bool writable)
    {
        return Registry.CurrentUser.OpenSubKey(RunKeyPath, writable)
            ?? (writable ? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true) : null);
    }
}

public enum StartupRegistrationAction
{
    None,
    Register,
    Unregister
}
