using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using Windows.UI;

namespace UnifiedMessenger.Services;

public static class ProfileAvatarService
{
    internal const string AvatarsFolderName = "avatars";

    internal const string AvatarFileExtension = ".png";

    private static string? s_cacheRootOverrideForTests;

    public static FrameworkElement CreateAvatar(MessengerInstance instance, double size = 28)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Avatar size must be greater than zero.");
        }

        var cachedPath = ResolveCachedAvatarPath(instance.Id);
        return cachedPath is not null
            ? CreateImageAvatar(cachedPath, size)
            : CreateInitialsAvatar(instance, size);
    }

    public static async Task SaveAvatarAsync(
        string instanceId,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            return;
        }

        var fileName = BuildAvatarFileName(instanceId);
        if (fileName is null)
        {
            return;
        }

        var directory = ResolveCacheRoot();
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, imageBytes, cancellationToken).ConfigureAwait(false);
    }

    public static Task RemoveAvatarAsync(string instanceId)
    {
        var path = ResolveCachedAvatarPath(instanceId);
        if (path is null)
        {
            return Task.CompletedTask;
        }

        File.Delete(path);
        return Task.CompletedTask;
    }

    internal static void SetCacheRootForTests(string? cacheRoot) => s_cacheRootOverrideForTests = cacheRoot;

    internal static string ResolveCacheRoot() =>
        s_cacheRootOverrideForTests
        ?? System.IO.Path.Combine(ApplicationPaths.UserDataRoot, AvatarsFolderName);

    internal static string? SanitizeInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var trimmed = instanceId.Trim();
        foreach (var character in trimmed)
        {
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_')
            {
                return null;
            }
        }

        return trimmed;
    }

    internal static string? BuildAvatarFileName(string instanceId)
    {
        var safeId = SanitizeInstanceId(instanceId);
        return safeId is null ? null : $"{safeId}{AvatarFileExtension}";
    }

    internal static string? ResolveCachedAvatarPath(string instanceId)
    {
        var fileName = BuildAvatarFileName(instanceId);
        if (fileName is null)
        {
            return null;
        }

        var path = System.IO.Path.Combine(ResolveCacheRoot(), fileName);
        return File.Exists(path) ? path : null;
    }

    private static FrameworkElement CreateInitialsAvatar(MessengerInstance instance, double size)
    {
        var brush = PlatformBrandingHelper.GetAccentBrush(instance);
        var host = new Grid
        {
            Width = size,
            Height = size
        };

        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush
        });

        host.Children.Add(new TextBlock
        {
            Text = PlatformBrandingHelper.GetInitials(instance.DisplayName),
            FontSize = size * 0.38,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        return host;
    }

    private static FrameworkElement CreateImageAvatar(string filePath, double size)
    {
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(filePath)),
                Stretch = Stretch.UniformToFill
            }
        };
    }
}
