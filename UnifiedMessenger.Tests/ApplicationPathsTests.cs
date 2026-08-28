using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ApplicationPathsTests
{
    [Fact]
    public void UserDataRoot_UsesLocalAppDataUnifiedMessenger()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationPaths.AppDataFolderName);

        // The whole suite runs with the root redirected to TEMP, because it used to write fabricated chats
        // into the developer's live oversight data (see TestIsolationTests). This test is about the
        // shipping formula, so it clears the redirect for the length of the assertion and puts it back.
        var restore = ApplicationPaths.UserDataRootOverrideForTests;
        try
        {
            ApplicationPaths.UserDataRootOverrideForTests = null;
            Assert.Equal(expected, ApplicationPaths.UserDataRoot);
        }
        finally
        {
            ApplicationPaths.UserDataRootOverrideForTests = restore;
        }
    }

    [Fact]
    public void SettingsFilePath_LivesUnderUserDataRoot()
    {
        Assert.Equal(Path.Combine(ApplicationPaths.UserDataRoot, "settings.json"), ApplicationPaths.SettingsFilePath);
    }
}
