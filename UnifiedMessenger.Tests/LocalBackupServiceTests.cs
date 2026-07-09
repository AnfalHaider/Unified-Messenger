using System.IO;
using System.Threading.Tasks;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class LocalBackupServiceTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "um-backup-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Backup_Then_Restore_RoundTrips_Stores_And_Avatars()
    {
        var source = NewTempDir();
        File.WriteAllText(Path.Combine(source, "settings.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(source, "instances.json"), "{\"b\":2}");
        File.WriteAllText(Path.Combine(source, "analytics.json"), "{\"c\":3}");
        var avatars = Path.Combine(source, "avatars");
        Directory.CreateDirectory(avatars);
        File.WriteAllText(Path.Combine(avatars, "one.png"), "img");

        // Excluded: a big session folder must not be backed up.
        var webview = Path.Combine(source, "WebView2");
        Directory.CreateDirectory(webview);
        File.WriteAllText(Path.Combine(webview, "cookies.bin"), "secret");

        var backupService = new LocalBackupService(source);
        Assert.Equal(4, backupService.CountBackupEntries()); // 3 json + 1 avatar

        var zipPath = Path.Combine(NewTempDir(), "backup.zip");
        var written = await backupService.CreateBackupAsync(zipPath);
        Assert.Equal(4, written);
        Assert.True(backupService.IsRecognisedBackup(zipPath));

        // Restore into a fresh, empty root.
        var target = NewTempDir();
        var restoreService = new LocalBackupService(target);
        var restored = await restoreService.RestoreAsync(zipPath);

        Assert.Equal(4, restored);
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(target, "settings.json")));
        Assert.Equal("{\"b\":2}", File.ReadAllText(Path.Combine(target, "instances.json")));
        Assert.Equal("img", File.ReadAllText(Path.Combine(target, "avatars", "one.png")));
        Assert.False(File.Exists(Path.Combine(target, "WebView2", "cookies.bin")));
    }

    [Fact]
    public void IsRecognisedBackup_False_ForNonBackupZip()
    {
        var dir = NewTempDir();
        var zipPath = Path.Combine(dir, "random.zip");
        using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a backup");
        }

        var service = new LocalBackupService(dir);
        Assert.False(service.IsRecognisedBackup(zipPath));
    }

    [Fact]
    public async Task Restore_Rejects_UnrecognisedArchive()
    {
        var dir = NewTempDir();
        var zipPath = Path.Combine(dir, "random.zip");
        using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("readme.txt");
        }

        var service = new LocalBackupService(NewTempDir());
        await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(zipPath));
    }
}
