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

        Assert.Equal(expected, ApplicationPaths.UserDataRoot);
    }

    [Fact]
    public void SettingsFilePath_LivesUnderUserDataRoot()
    {
        Assert.Equal(Path.Combine(ApplicationPaths.UserDataRoot, "settings.json"), ApplicationPaths.SettingsFilePath);
    }
}
