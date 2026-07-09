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

    [Fact]
    public void TryShutdownOnWindowClosed_WhenRunInBackground_DoesNotThrow() =>
        ApplicationLifecycleService.TryShutdownOnWindowClosed(
            forceShutdown: false,
            runInBackgroundOnClose: true);
}
