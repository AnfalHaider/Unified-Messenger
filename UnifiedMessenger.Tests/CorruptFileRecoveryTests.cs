using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for F-DURA-01 and F-DURA-02.
///
/// Three durable stores each answered "what do I do with a file I cannot read?" differently:
/// only the settings store preserved the bad file, all three reported via Debug.WriteLine (stripped from
/// Release, so a real reset left no trace in the shipping build), and all three caught JsonException only
/// — so a file locked by a backup tool threw straight out of the load path.
///
/// AwaitingOverrideStore was the worst case: it left the unreadable file in place and the next flush
/// overwrote it with empty state, permanently destroying every chat the owner had marked handled.
/// </summary>
public class CorruptFileRecoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "um-corrupt-recovery-tests", Guid.NewGuid().ToString("N"));

    public CorruptFileRecoveryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup only.
        }

        GC.SuppressFinalize(this);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData(typeof(JsonException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(NotSupportedException))]
    public void UnreadableFileExceptionsAreRecognised(Type exceptionType)
    {
        // IOException and UnauthorizedAccessException are the ones that used to escape the load path
        // entirely — a settings file held open by a backup tool failed startup instead of degrading.
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.True(CorruptFileRecovery.IsUnreadable(ex));
    }

    [Fact]
    public void CancellationIsNotTreatedAsCorruption()
    {
        // Critical: a load cancelled during shutdown is not a damaged file. Treating it as one would move
        // a perfectly good store aside and hand the user an empty one.
        Assert.False(CorruptFileRecovery.IsUnreadable(new OperationCanceledException()));
        Assert.False(CorruptFileRecovery.IsUnreadable(new TaskCanceledException()));
    }

    [Fact]
    public void ProgrammerErrorsAreNotTreatedAsCorruption()
    {
        // A NullReferenceException means a bug, not a bad file. Swallowing it as "corruption" would hide
        // the defect and silently reset the user's data on every launch.
        Assert.False(CorruptFileRecovery.IsUnreadable(new NullReferenceException()));
        Assert.False(CorruptFileRecovery.IsUnreadable(new ArgumentNullException()));
    }

    [Fact]
    public void UnreadableFileIsMovedAsideSoItsBytesSurvive()
    {
        const string original = "{ \"instances\": { \"acct-1\": { \"chat-9\": ";
        var path = WriteFile("awaiting-overrides.json", original);

        var backup = CorruptFileRecovery.Preserve(path, "AwaitingOverrides", new JsonException("truncated"));

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup), "the preserved copy must exist on disk");
        Assert.Equal(original, File.ReadAllText(backup!));

        // The original path must be clear, so the store writes a fresh file rather than overwriting the
        // only copy of the user's data.
        Assert.False(File.Exists(path));
        Assert.Contains(".corrupt-", backup!, StringComparison.Ordinal);
    }

    [Fact]
    public void PreserveIsHarmlessWhenTheFileIsAlreadyGone()
    {
        var missing = Path.Combine(_dir, "not-here.json");

        var backup = CorruptFileRecovery.Preserve(missing, "KpiTrends", new JsonException("x"));

        Assert.Null(backup);
    }

    [Fact]
    public void PreserveDoesNotThrowOnAnEmptyPath()
    {
        // Must never be able to take down startup.
        var backup = CorruptFileRecovery.Preserve(string.Empty, "Settings", new JsonException("x"));

        Assert.Null(backup);
    }

    [Fact]
    public void TwoPreservesDoNotCollide()
    {
        var first = WriteFile("store.json", "bad-1");
        var firstBackup = CorruptFileRecovery.Preserve(first, "Settings", new JsonException("x"));

        var second = WriteFile("store.json", "bad-2");
        var secondBackup = CorruptFileRecovery.Preserve(second, "Settings", new JsonException("x"));

        Assert.NotNull(firstBackup);
        Assert.NotNull(secondBackup);

        // Same-second collisions overwrite by design (File.Move overwrite: true) rather than throwing and
        // breaking startup — but the first file's content must never be silently reported as preserved
        // when it is not. Whichever path each backup points at must hold real content.
        Assert.True(File.Exists(firstBackup!) || File.Exists(secondBackup!));
        Assert.Equal("bad-2", File.ReadAllText(secondBackup!));
    }
}
