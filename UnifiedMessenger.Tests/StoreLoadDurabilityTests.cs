using System.Text.RegularExpressions;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.Services.Shell;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for T-01 and T-02.
///
/// <para>
/// F-DURA-01/02 centralised "what do I do with a file I cannot read?" into
/// <see cref="CorruptFileRecovery"/> and applied it to six stores. Five were missed, and they were the five
/// loaded from <see cref="ShellController.InitializeAsync"/>:
/// </para>
/// <list type="bullet">
/// <item><c>ResponseTimeTracker</c> and <c>ContactHistoryStore</c> logged the corruption and returned, so
/// the next debounced save wrote empty state over the unreadable file and destroyed it. Losing
/// <c>response-times.json</c> silently resets median reply time, SLA met % and the response-time trend —
/// figures the dashboard then shows with no sign anything happened.</item>
/// <item><c>MessageAnalyticsService</c> and <c>OversightChatSnapshotService</c> preserved the file but
/// caught <c>JsonException</c> only.</item>
/// <item><c>BackfillDedupeStore</c> had no handler at all.</item>
/// </list>
/// <para>
/// Because those loads ran unguarded at the top of <c>InitializeAsync</c>, an <see cref="IOException"/> —
/// a backup tool or antivirus holding a file open for a moment, a profile on an unreachable network path —
/// propagated to <c>App.LaunchAsync</c>, which shows "The application could not start." and exits. A
/// statistics file being briefly locked stopped the owner opening the app that holds their accounts.
/// </para>
/// </summary>
public class StoreLoadDurabilityTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "um-store-durability-tests", Guid.NewGuid().ToString("N"));

    public StoreLoadDurabilityTests() => Directory.CreateDirectory(_dir);

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

    private const string Corrupt = "{ this is not json";

    private string WriteCorruptStore(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, Corrupt);
        return path;
    }

    private string[] PreservedCopies(string storePath) =>
        Directory.GetFiles(_dir, $"{Path.GetFileName(storePath)}.corrupt-*.bak");

    /// <summary>
    /// The bytes must survive. A store that resets without preserving hands the file to its own next
    /// flush to overwrite, and the owner's history is then gone with no copy to repair by hand.
    /// </summary>
    [Fact]
    public async Task ResponseTimeHistoryIsPreservedWhenItCannotBeRead()
    {
        var path = WriteCorruptStore("response-times.json");

        var tracker = new ResponseTimeTracker(path);
        await tracker.LoadAsync();

        var preserved = PreservedCopies(path);
        Assert.Single(preserved);
        Assert.Equal(Corrupt, File.ReadAllText(preserved[0]));
    }

    [Fact]
    public async Task ContactHistoryIsPreservedWhenItCannotBeRead()
    {
        var path = WriteCorruptStore("contact-history.json");

        var store = new ContactHistoryStore(path);
        await store.LoadAsync();

        var preserved = PreservedCopies(path);
        Assert.Single(preserved);
        Assert.Equal(Corrupt, File.ReadAllText(preserved[0]));
    }

    [Fact]
    public async Task AnalyticsIsPreservedWhenItCannotBeRead()
    {
        var path = WriteCorruptStore("analytics.json");

        var service = new MessageAnalyticsService(path);
        await service.LoadAsync();

        Assert.Single(PreservedCopies(path));
    }

    [Fact]
    public async Task OversightSnapshotIsPreservedWhenItCannotBeRead()
    {
        var path = WriteCorruptStore("oversight-snapshot.json");

        var service = new OversightChatSnapshotService(path);
        await service.LoadAsync();

        Assert.Single(PreservedCopies(path));
    }

    /// <summary>
    /// A file another process holds open exclusively is the case that used to escape every one of these
    /// load paths, because they caught <c>JsonException</c> and nothing else.
    /// </summary>
    [Theory]
    [InlineData("response-times.json")]
    [InlineData("contact-history.json")]
    [InlineData("analytics.json")]
    [InlineData("oversight-snapshot.json")]
    [InlineData("backfill-dedupe.json")]
    public async Task ALockedStoreFileDoesNotThrowOutOfTheLoad(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "{}");

        // FileShare.None is what a backup tool or antivirus holds while it reads the file.
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Func<Task> load = name switch
        {
            "response-times.json" => () => new ResponseTimeTracker(path).LoadAsync(),
            "contact-history.json" => () => new ContactHistoryStore(path).LoadAsync(),
            "analytics.json" => () => new MessageAnalyticsService(path).LoadAsync(),
            "oversight-snapshot.json" => () => new OversightChatSnapshotService(path).LoadAsync(),
            _ => () => new BackfillDedupeStore(path)
                .TryAcceptForDayAsync("acct", "whatsapp", "923000000000@c.us", DateTimeOffset.UtcNow),
        };

        var failure = await Record.ExceptionAsync(load);

        Assert.Null(failure);
    }

    /// <summary>
    /// The per-store fixes are individually correct but individually forgettable — a store added to
    /// <c>InitializeAsync</c> later would not inherit them. <c>LoadStoreAsync</c> makes it structural.
    /// </summary>
    [Fact]
    public async Task TheShellAbsorbsAStoreThatFailsToLoad()
    {
        var failure = await Record.ExceptionAsync(() =>
            ShellController.LoadStoreAsync("Test.Store", _ => throw new IOException("held open by another process")));

        Assert.Null(failure);
    }

    [Fact]
    public async Task TheShellStillRunsAStoreThatLoadsCleanly()
    {
        var ran = false;

        await ShellController.LoadStoreAsync("Test.Store", _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
    }

    /// <summary>
    /// Source-level guard. The runtime tests above prove the eleven stores that exist today are safe; this
    /// one proves the twelfth will be too, by refusing a bare <c>await …LoadAsync()</c> in the startup
    /// sequence. Settings and the account registry are exempt: both carry their own recovery, and a failure
    /// to read the account list is not something to shrug off and continue past.
    /// </summary>
    [Fact]
    public void EveryAuxiliaryStoreLoadInStartupGoesThroughTheGuard()
    {
        var source = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "Services", "Shell", "ShellController.cs"));

        var start = source.IndexOf("public async Task InitializeAsync()", StringComparison.Ordinal);
        Assert.True(start > 0, "InitializeAsync could not be found in ShellController.cs");

        // The store loads are the opening block, ending where the shell starts touching UI.
        var end = source.IndexOf("_chrome.PanePinned", start, StringComparison.Ordinal);
        Assert.True(end > start, "The startup store-load block could not be delimited");

        var unguarded = Regex.Matches(source[start..end], @"await\s+(?!LoadStoreAsync)([\w\._]+)\.LoadAsync\(")
            .Select(m => m.Groups[1].Value)
            .Where(target => target is not ("_services.AppSettings" or "_services.Registry"))
            .ToList();

        Assert.True(
            unguarded.Count == 0,
            "These startup store loads bypass ShellController.LoadStoreAsync, so an unreadable file would "
            + "stop the app from opening at all: " + string.Join(", ", unguarded));
    }
}
