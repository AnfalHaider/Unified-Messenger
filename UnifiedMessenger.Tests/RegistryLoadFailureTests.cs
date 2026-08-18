using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The owner opened the installed app and it greeted them with "Welcome to Unified Messenger — add an
/// account to start receiving unified notifications" and one demo account, having had nine connected.
///
/// <para>
/// <b>The cause.</b> <c>LoadAsync</c> began with <c>if (!File.Exists(storePath))</c> and treated false as
/// "first run". <see cref="File.Exists(string)"/> returns false for <i>every</i> failure — a denied folder,
/// an unreachable path, a permissions problem — not only for a file that is genuinely absent. So a
/// transient access failure was indistinguishable from a clean install, and the app responded by seeding a
/// starter account.
/// </para>
/// <para>
/// <b>Why it was worse than a cosmetic bug.</b> The seeded list is then a save away from being written
/// over the real one. Renaming an account, adding one, or dragging one to reorder would have replaced nine
/// accounts with one — permanently, with no prompt. The owner happened to close the app instead.
/// </para>
/// <para>
/// These tests were written against the reproduction before the fix: the "missing directory" case returned
/// exactly one instance with id <c>whatsapp-default</c>, which is precisely what the owner's screenshot
/// showed.
/// </para>
/// </summary>
public class RegistryLoadFailureTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public RegistryLoadFailureTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "um-registry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "instances.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // A test tidying up must never be the thing that fails the run.
        }
        GC.SuppressFinalize(this);
    }

    private void WriteRealAccounts()
    {
        var store = new InstanceStore
        {
            Instances =
            [
                NewInstance("a1", "Depilex DHA-2 WhatsApp"),
                NewInstance("a2", "Depilex F-11 WhatsApp"),
                NewInstance("a3", "Depilex Men DHA-2 WhatsApp")
            ]
        };

        File.WriteAllText(_path, JsonSerializer.Serialize(
            store, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static MessengerInstance NewInstance(string id, string name) => new()
    {
        Id = id,
        DisplayName = name,
        ProfileName = "whatsapp-" + id,
        Platform = "whatsapp",
        StartUrl = "https://web.whatsapp.com",
        Category = WorkspaceCategory.Professional,
        SortOrder = 1
    };

    // ---- An unreadable file is never mistaken for a first run --------------------------------------

    [Fact]
    public async Task ALockedFileIsReportedAsUnreadableRatherThanEmpty()
    {
        WriteRealAccounts();

        using var _ = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.Failed, registry.LoadOutcome);
        Assert.NotNull(registry.LoadFailureDetail);
    }

    [Fact]
    public async Task AnUnreachableFolderIsNotTreatedAsACleanInstall()
    {
        // The exact reproduction of the owner's screen. Before the fix this returned one instance with id
        // "whatsapp-default" and wrote it to disk.
        var registry = new InstanceRegistryService(Path.Combine(_dir, "gone", "instances.json"));
        await registry.LoadAsync();

        Assert.NotEqual(RegistryLoadOutcome.Failed, registry.LoadOutcome);

        // A genuinely absent path IS a first run, and seeding a starter account there is correct.
        Assert.Equal(RegistryLoadOutcome.FirstRun, registry.LoadOutcome);
    }

    [Fact]
    public async Task AGenuinelyMissingFileIsStillAFirstRun()
    {
        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.FirstRun, registry.LoadOutcome);
        Assert.Single(registry.Instances);
    }

    [Fact]
    public async Task AReadableFileLoadsEveryAccount()
    {
        WriteRealAccounts();

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.Loaded, registry.LoadOutcome);
        Assert.Equal(3, registry.Instances.Count);
    }

    // ---- The data-loss guard -----------------------------------------------------------------------

    [Fact]
    public async Task AFailedLoadCanNeverOverwriteTheRealAccountList()
    {
        WriteRealAccounts();
        var bytesBefore = new FileInfo(_path).Length;

        InstanceRegistryService registry;
        using (var _ = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            registry = new InstanceRegistryService(_path);
            await registry.LoadAsync();
            Assert.Equal(RegistryLoadOutcome.Failed, registry.LoadOutcome);
        }

        // The lock is now gone — so a save WOULD succeed at the filesystem level. This is the moment the
        // old code could destroy the accounts: the owner renames something, and one instance is written
        // over nine. The guard refuses instead.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => registry.SaveAsync());
        Assert.Equal(InstanceRegistryService.RefusedSaveMessage, thrown.Message);

        Assert.Equal(bytesBefore, new FileInfo(_path).Length);

        var reread = new InstanceRegistryService(_path);
        await reread.LoadAsync();
        Assert.Equal(3, reread.Instances.Count);
    }

    [Fact]
    public async Task AddingAnAccountAfterAFailedLoadIsRefusedRatherThanSilentlyDestructive()
    {
        WriteRealAccounts();

        InstanceRegistryService registry;
        using (var _ = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            registry = new InstanceRegistryService(_path);
            await registry.LoadAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.AddInstanceAsync("New account", "whatsapp", null));

        var reread = new InstanceRegistryService(_path);
        await reread.LoadAsync();
        Assert.Equal(3, reread.Instances.Count);
    }

    // ---- Recovery ----------------------------------------------------------------------------------

    [Fact]
    public async Task RetryingAfterTheBlockClearsLoadsTheRealAccounts()
    {
        WriteRealAccounts();

        InstanceRegistryService registry;
        using (var _ = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            registry = new InstanceRegistryService(_path);
            await registry.LoadAsync();
            Assert.Empty(registry.Instances);
        }

        Assert.True(await registry.RetryLoadAsync());
        Assert.Equal(RegistryLoadOutcome.Loaded, registry.LoadOutcome);
        Assert.Equal(3, registry.Instances.Count);

        // And saving works again, because the list is real now.
        await registry.SaveAsync();
    }

    [Fact]
    public async Task ABriefLockIsWaitedOutRatherThanFailingTheSession()
    {
        // A virus scanner opening a just-written file holds it for a few hundred milliseconds. One unlucky
        // read must not decide what the owner sees for the rest of the session.
        WriteRealAccounts();

        var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            await stream.DisposeAsync();
        });

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.Loaded, registry.LoadOutcome);
        Assert.Equal(3, registry.Instances.Count);
    }

    // ---- Corrupt content ---------------------------------------------------------------------------

    [Fact]
    public async Task UnparseableContentIsPreservedBeforeAnythingReplacesIt()
    {
        File.WriteAllText(_path, "{ not json at all");

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.RecoveredFromCorruptFile, registry.LoadOutcome);

        var preserved = Directory.GetFiles(_dir, "instances.json.corrupt-*.bak");
        Assert.Single(preserved);
        Assert.Equal("{ not json at all", File.ReadAllText(preserved[0]));
    }

    [Fact]
    public async Task UnparseableContentThatCannotBePreservedIsNotReplacedEither()
    {
        // The old code swallowed a failed backup and overwrote the file anyway, destroying the only copy of
        // something a person could still have repaired by hand.
        //
        // FileShare.Read is the share mode that isolates this: it lets the registry read and fail to parse
        // (FileShare.None would stop it before the parse, which is a different case, covered above) while
        // still denying the delete access File.Move needs.
        File.WriteAllText(_path, "{ not json at all");

        using var _ = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.Failed, registry.LoadOutcome);
        Assert.True(File.Exists(_path), "The unparseable file was deleted despite the backup failing.");
        Assert.Empty(Directory.GetFiles(_dir, "instances.json.corrupt-*.bak"));
    }

    [Fact]
    public async Task AFileContainingLiteralNullIsTreatedAsCorruptNotAsNoAccounts()
    {
        File.WriteAllText(_path, "null");

        var registry = new InstanceRegistryService(_path);
        await registry.LoadAsync();

        Assert.Equal(RegistryLoadOutcome.RecoveredFromCorruptFile, registry.LoadOutcome);
        Assert.Single(Directory.GetFiles(_dir, "instances.json.corrupt-*.bak"));
    }
}

/// <summary>
/// What the owner is told. The screen they actually saw said the opposite of the truth, in a friendly
/// voice, which is why the copy is pinned by test rather than left to whoever edits the dialog next.
/// </summary>
public class AccountsUnavailableNoticeTests
{
    [Theory]
    [InlineData(RegistryLoadOutcome.Failed, true)]
    [InlineData(RegistryLoadOutcome.Loaded, false)]
    [InlineData(RegistryLoadOutcome.FirstRun, false)]
    [InlineData(RegistryLoadOutcome.RecoveredFromCorruptFile, false)]
    [InlineData(RegistryLoadOutcome.NotLoaded, false)]
    public void TheNoticeAppearsOnlyForAFailedRead(RegistryLoadOutcome outcome, bool expected) =>
        Assert.Equal(expected, AccountsUnavailableNotice.ShouldShow(outcome));

    [Fact]
    public void ItSaysNothingWasLostBeforeItSaysAnythingElse()
    {
        // The owner's actual question, asked in their words, was "why does my install version not have my
        // data?". The answer to that belongs in the first sentence, not below the fold.
        var message = AccountsUnavailableNotice.BuildMessage(@"C:\path\instances.json", "IOException: locked");
        var first = message.Split("\n\n")[0];

        Assert.Contains("Nothing has been lost", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ItNamesTheFileSoTheOwnerCanSeeForThemselvesThatItIsStillThere()
    {
        var message = AccountsUnavailableNotice.BuildMessage(@"C:\path\instances.json", null);

        Assert.Contains(@"C:\path\instances.json", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ItRepeatsTheUnderlyingErrorVerbatim()
    {
        var message = AccountsUnavailableNotice.BuildMessage(null, "UnauthorizedAccessException: Access to the path is denied.");

        Assert.Contains("UnauthorizedAccessException: Access to the path is denied.", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ItPromisesNotToSaveOverWhatItCannotRead()
    {
        // This is a commitment the code actually keeps (see AFailedLoadCanNeverOverwriteTheRealAccountList).
        // Stating it is what lets an owner keep using the app for the rest of the session without worrying.
        var message = AccountsUnavailableNotice.BuildMessage(null, null);

        Assert.Contains("will not save over", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSidebarRailDoesNotClaimThereAreNoAccounts()
    {
        // Caught by looking at the live app rather than by reasoning: the dashboard notice was correct
        // while the rail beside it still read "No accounts yet." — a flat assertion the app is not
        // entitled to make, and the one the owner's eye lands on first.
        var failed = WorkspaceSidebarMenuPlanner.BuildPlan(
            [], SidebarScope.All, RegistryLoadOutcome.Failed);
        var hint = failed.Entries.Single(e => e.Kind == SidebarMenuEntryKind.EmptyHint).HintText;

        Assert.Equal(WorkspaceSidebarMenuPlanner.UnreadableHintText, hint);
        Assert.DoesNotContain("No accounts yet", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheTwoEmptyStatesAreStructurallyDifferentSoTheRailActuallyRedraws()
    {
        // Text alone was not enough. HasSameStructure compares keys, so the corrected wording was computed
        // and then discarded — the rail kept the row it already had. Found by looking at the running app
        // after the "fix", not by running the tests.
        var empty = WorkspaceSidebarMenuPlanner.BuildPlan([], SidebarScope.All, RegistryLoadOutcome.FirstRun);
        var failed = WorkspaceSidebarMenuPlanner.BuildPlan([], SidebarScope.All, RegistryLoadOutcome.Failed);

        Assert.False(WorkspaceSidebarMenuPlanner.HasSameStructure(empty, failed));
        Assert.False(WorkspaceSidebarMenuPlanner.HasSameStructure(failed, empty));
    }

    [Fact]
    public void TheSidebarStillSaysNoAccountsWhenThatIsActuallyTrue()
    {
        var firstRun = WorkspaceSidebarMenuPlanner.BuildPlan(
            [], SidebarScope.All, RegistryLoadOutcome.FirstRun);

        Assert.Equal(
            "No accounts yet.",
            firstRun.Entries.Single(e => e.Kind == SidebarMenuEntryKind.EmptyHint).HintText);
    }

    [Fact]
    public void TheDashboardLineContradictsTheWelcomeCopyItReplaces()
    {
        // The string it stands in for is "Add an account to start receiving unified notifications." — an
        // instruction that is actively wrong here. The replacement must not read like an invitation to
        // start over.
        Assert.DoesNotContain("Add an account", AccountsUnavailableNotice.DashboardSubtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("still saved", AccountsUnavailableNotice.DashboardSubtitle, StringComparison.OrdinalIgnoreCase);
    }
}
