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
    [ModuleInitializer]
    internal static void Initialize() => AppLogger.SuppressWritesForTests = true;
}
