using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Tests;

public class PlatformBrandingHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    public void ParseAccentColor_ReturnsDefaultForInvalidInput(string? hex)
    {
        var color = PlatformBrandingHelper.ParseAccentColor(hex);

        Assert.Equal(107, color.R);
        Assert.Equal(114, color.G);
        Assert.Equal(128, color.B);
        Assert.Equal(255, color.A);
    }

    [Theory]
    [InlineData("#25D366", 37, 211, 102)]
    [InlineData("25D366", 37, 211, 102)]
    [InlineData("#F00", 255, 0, 0)]
    [InlineData("#80FF0000", 255, 0, 0, 128)]
    public void ParseAccentColor_ParsesSupportedHexFormats(
        string hex,
        byte expectedR,
        byte expectedG,
        byte expectedB,
        byte expectedA = 255)
    {
        var color = PlatformBrandingHelper.ParseAccentColor(hex);

        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
        Assert.Equal(expectedA, color.A);
    }

    [Theory]
    [InlineData(null, "?")]
    [InlineData("   ", "?")]
    [InlineData("Alex", "AL")]
    [InlineData("A", "A")]
    [InlineData("Alex Morgan", "AM")]
    [InlineData("  sam   lee  ", "SL")]
    public void GetInitials_FormatsDisplayName(string? displayName, string expected)
    {
        Assert.Equal(expected, PlatformBrandingHelper.GetInitials(displayName));
    }
}

public class ProfileAvatarServiceTests : IDisposable
{
    private readonly string _cacheRoot;

    public ProfileAvatarServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), $"um-avatars-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_cacheRoot);
        ProfileAvatarService.SetCacheRootForTests(_cacheRoot);
    }

    public void Dispose()
    {
        ProfileAvatarService.SetCacheRootForTests(null);

        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape")]
    [InlineData("bad/id")]
    public void SanitizeInstanceId_RejectsUnsafeValues(string? instanceId)
    {
        Assert.Null(ProfileAvatarService.SanitizeInstanceId(instanceId!));
    }

    [Fact]
    public void SanitizeInstanceId_AllowsStableFileNames()
    {
        Assert.Equal("inst-whatsapp_1", ProfileAvatarService.SanitizeInstanceId(" inst-whatsapp_1 "));
    }

    [Fact]
    public async Task SaveAvatarAsync_WritesCachedAvatarForLookup()
    {
        await ProfileAvatarService.SaveAvatarAsync("inst-1", [0x89, 0x50, 0x4E, 0x47]);

        var cachedPath = ProfileAvatarService.ResolveCachedAvatarPath("inst-1");

        Assert.NotNull(cachedPath);
        Assert.True(File.Exists(cachedPath));
        Assert.EndsWith("inst-1.png", cachedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveAvatarAsync_DeletesCachedAvatar()
    {
        await ProfileAvatarService.SaveAvatarAsync("inst-2", [1, 2, 3]);

        await ProfileAvatarService.RemoveAvatarAsync("inst-2");

        Assert.Null(ProfileAvatarService.ResolveCachedAvatarPath("inst-2"));
    }
}
