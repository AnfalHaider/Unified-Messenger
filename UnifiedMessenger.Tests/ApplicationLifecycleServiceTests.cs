using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ApplicationLifecycleServiceTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShouldHideOnClose_RespectsForceShutdownAndSetting(
        bool forceShutdown,
        bool runInBackground,
        bool expected) =>
        Assert.Equal(
            expected,
            ApplicationLifecycleService.ShouldHideOnClose(forceShutdown, runInBackground));
}
