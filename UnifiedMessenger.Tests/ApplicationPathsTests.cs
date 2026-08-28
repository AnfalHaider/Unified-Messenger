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
        // into the developer's live oversight data (see TestIsolationTests). Asserted against
        // DefaultUserDataRoot rather than by clearing the redirect: xUnit runs test classes in parallel, so
        // clearing it — even with a finally to put it back — hands the real user-data root to whatever else
        // happens to resolve a store path in that window. The first version of this test did that and
        // TriagePersistenceServiceTests failed on the very next run.
        Assert.Equal(expected, ApplicationPaths.DefaultUserDataRoot);
    }

    [Fact]
    public void SettingsFilePath_LivesUnderUserDataRoot()
    {
        Assert.Equal(Path.Combine(ApplicationPaths.UserDataRoot, "settings.json"), ApplicationPaths.SettingsFilePath);
    }
}
