using System.Runtime.CompilerServices;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Runs once, before any test in this assembly.
/// </summary>
internal static class TestAssemblyInit
{
    /// <summary>
    /// Stops the test suite writing to the developer's real diagnostic log.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AppLogger"/> writes to a fixed path under the real user-data root
    /// (<c>%LOCALAPPDATA%\UnifiedMessenger\app.log</c>), so any test that exercises production code
    /// containing a log call appends to it. Found in a live log during this audit, every line fabricated by
    /// this suite and none of it true of any user:
    /// </para>
    /// <code>
    /// [ERR] [Lifecycle.Flush.third]          IOException: third could not be written
    /// [ERR] [Settings.Load.Corrupt]          JsonException: x
    /// [ERR] [AwaitingOverrides.Load.Corrupt] JsonException: truncated
    /// [WRN] [ChatEntryParser]                Skipped 1 of 2 conversation rows as unparseable.
    /// </code>
    /// <para>
    /// A module initializer is used deliberately: it needs no per-class opt-in, so a suite added later
    /// cannot forget it. The first attempt at this fix threaded a log callback through a single method and
    /// missed every other logging call — three of the four lines above come from suites it did not touch.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Points every durable store at a throwaway folder, so the suite cannot write into live oversight data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The log fix above stopped at <c>app.log</c>. The same disease was still live for every *data* store:
    /// the suite uses the real singletons (<c>OversightChatSnapshotService.Instance</c> and friends), and
    /// those resolve their paths from <see cref="ApplicationPaths.UserDataRoot"/>. So
    /// <c>AwaitingSplitTests</c> calling <c>svc.Update(...)</c> wrote fabricated chats straight into the
    /// developer's own store — a scan of the real one found the test id <c>inst-1</c> filed beside the real
    /// accounts.
    /// </para>
    /// <para>
    /// The expensive part was silent: that same <c>Update</c> reaches
    /// <c>ResponseTimeTracker.Observe</c>, which stamps each account's watch start on first sight and only
    /// measures replies to messages arriving after it. Every suite run reset that stamp, so reply-time
    /// samples could never accumulate — 761 KB of snapshot, 218 KB of contact history, zero samples — and
    /// SLA compliance was computed and displayed from that emptiness.
    /// </para>
    /// <para>
    /// Redirecting the root covers every store at once, including ones not written yet. The folder is left
    /// on disk: it is small, it is under TEMP, and deleting it from a module initializer would race the
    /// stores' own debounced saves still finishing as the process exits.
    /// </para>
    /// </remarks>
    [ModuleInitializer]
    internal static void Initialize()
    {
        AppLogger.SuppressWritesForTests = true;

        var root = Path.Combine(
            Path.GetTempPath(),
            "UnifiedMessengerTests",
            "user-data",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        ApplicationPaths.UserDataRootOverrideForTests = root;
    }
}
