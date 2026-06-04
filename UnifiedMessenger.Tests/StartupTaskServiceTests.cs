using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class StartupTaskServiceTests
{
    [Fact]
    public void BuildRunValue_QuotesExecutablePath()
    {
        Assert.Equal(
            "\"C:\\Apps\\UnifiedMessenger.exe\"",
            StartupTaskService.BuildRunValue(@"C:\Apps\UnifiedMessenger.exe"));
    }

    [Theory]
    [InlineData("\"C:\\Apps\\UnifiedMessenger.exe\"", @"C:\Apps\UnifiedMessenger.exe")]
    [InlineData("C:\\Apps\\UnifiedMessenger.exe", @"C:\Apps\UnifiedMessenger.exe")]
    public void TryParseRunValue_ParsesQuotedAndPlainValues(string value, string expectedPath)
    {
        Assert.True(StartupTaskService.TryParseRunValue(value, out var path));
        Assert.Equal(expectedPath, path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseRunValue_RejectsEmptyValues(string? value)
    {
        Assert.False(StartupTaskService.TryParseRunValue(value, out _));
    }

    [Fact]
    public void PathsEqual_IgnoresCaseAndNormalizesSeparators()
    {
        Assert.True(StartupTaskService.PathsEqual(
            @"c:\apps\unified messenger.exe",
            @"C:\Apps\Unified Messenger.exe"));
    }

    [Theory]
    [InlineData(true, null, @"C:\Apps\UnifiedMessenger.exe", StartupRegistrationAction.Register)]
    [InlineData(
        true,
        "\"C:\\Apps\\UnifiedMessenger.exe\"",
        @"C:\Apps\UnifiedMessenger.exe",
        StartupRegistrationAction.None)]
    [InlineData(
        true,
        "\"C:\\Old\\UnifiedMessenger.exe\"",
        @"C:\Apps\UnifiedMessenger.exe",
        StartupRegistrationAction.Register)]
    [InlineData(
        false,
        "\"C:\\Apps\\UnifiedMessenger.exe\"",
        @"C:\Apps\UnifiedMessenger.exe",
        StartupRegistrationAction.Unregister)]
    [InlineData(false, null, @"C:\Apps\UnifiedMessenger.exe", StartupRegistrationAction.None)]
    public void ResolveRegistrationAction_PlanSyncSteps(
        bool launchAtStartup,
        string? registeredRunValue,
        string currentExecutablePath,
        StartupRegistrationAction expected)
    {
        Assert.Equal(
            expected,
            StartupTaskService.ResolveRegistrationAction(
                launchAtStartup,
                registeredRunValue,
                currentExecutablePath));
    }
}
