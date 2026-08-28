using System.Text.RegularExpressions;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for T-21 — flipping a Settings toggle could close the app.
///
/// <para>
/// <c>SaveCoreAsync</c> let an <see cref="IOException"/> out, and roughly thirty of the Settings handlers
/// are <c>async void</c> event handlers that call it with no <c>try</c> of their own. An exception from an
/// <c>async void</c> handler reaches <c>App.OnUnhandledException</c>, which deliberately leaves
/// <c>Handled=false</c> so genuinely unrecoverable faults end the process — so a toggle flipped while the
/// settings file was locked by antivirus, a backup tool, or a full disk simply closed the app. None of
/// those is the owner's fault and none of them is unrecoverable.
/// </para>
/// <para>
/// The same call is also made fire-and-forget in two places (<c>_ = UpdateAsync(…)</c>), where the failure
/// was not fatal but was completely silent: the preference did not persist and nothing said so.
/// </para>
/// <para>
/// Fixing it at the thirty call sites would have left the thirty-first to be written without the guard, so
/// it is fixed once in the service — which absorbs the write failure, records it, logs it, and raises it
/// for the shell to show. Absorbing without reporting would have replaced a crash with a lie.
/// </para>
/// </summary>
public class SettingsSaveFailureTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "um-settings-save-tests", Guid.NewGuid().ToString("N"));

    private readonly string _storePath;

    public SettingsSaveFailureTests()
    {
        Directory.CreateDirectory(_dir);
        _storePath = Path.Combine(_dir, "settings.json");
    }

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

    /// <summary>
    /// Holds the temp file the atomic write goes through, the way a backup tool or scanner holds a file
    /// mid-write. <c>FileShare.None</c> is what makes the service's own <c>FileStream</c> fail.
    /// </summary>
    private FileStream BlockTheWrite() =>
        new(_storePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None);

    [Fact]
    public async Task AnUnwritableSettingsFileDoesNotThrowOutOfTheHandler()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        using var blocker = BlockTheWrite();

        var failure = await Record.ExceptionAsync(() =>
            service.UpdateAsync(s => s.RunInBackgroundOnClose = false));

        Assert.Null(failure);
    }

    [Fact]
    public async Task TheOwnerIsToldTheSettingWasNotSaved()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        var reported = new List<string>();
        service.SaveFailed += (_, reason) => reported.Add(reason);

        using (BlockTheWrite())
        {
            await service.UpdateAsync(s => s.RunInBackgroundOnClose = false);
        }

        Assert.Single(reported);
        Assert.False(string.IsNullOrWhiteSpace(reported[0]));
        Assert.Equal(reported[0], service.LastSaveFailure);
    }

    /// <summary>
    /// Once per run of failures, not once per attempt. A NumberBox being dragged while the file is jammed
    /// would otherwise raise a dialog per value change.
    /// </summary>
    [Fact]
    public async Task RepeatedFailuresAreReportedOnce()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        var reported = 0;
        service.SaveFailed += (_, _) => reported++;

        using (BlockTheWrite())
        {
            for (var i = 0; i < 5; i++)
            {
                await service.UpdateAsync(s => s.DashboardUrgencyThreshold = 30 + i);
            }
        }

        Assert.Equal(1, reported);
    }

    /// <summary>
    /// And it must go quiet again, or the next genuine failure would be indistinguishable from the stale
    /// one and would never be raised.
    /// </summary>
    [Fact]
    public async Task ASuccessfulSaveClearsTheFailureAndRearmsTheReport()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        var reported = 0;
        service.SaveFailed += (_, _) => reported++;

        using (BlockTheWrite())
        {
            await service.UpdateAsync(s => s.RunInBackgroundOnClose = false);
        }

        await service.UpdateAsync(s => s.RunInBackgroundOnClose = true);
        Assert.Null(service.LastSaveFailure);

        using (BlockTheWrite())
        {
            await service.UpdateAsync(s => s.RunInBackgroundOnClose = false);
        }

        Assert.Equal(2, reported);
    }

    [Fact]
    public async Task AWritableFileStillSavesAndReportsNothing()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        var reported = 0;
        service.SaveFailed += (_, _) => reported++;

        await service.UpdateAsync(s => s.DashboardUrgencyThreshold = 42);

        Assert.Equal(0, reported);
        Assert.Null(service.LastSaveFailure);

        var reloaded = new AppSettingsService(_storePath);
        await reloaded.LoadAsync();
        Assert.Equal(42, reloaded.Settings.DashboardUrgencyThreshold);
    }

    /// <summary>
    /// A programmer error inside the mutate callback is not a write failure and must still surface. Turning
    /// the settings service into a catch-all would hide real bugs behind "your settings could not be saved".
    /// </summary>
    [Fact]
    public async Task AFaultInTheCallerIsNotSwallowed()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(_ => throw new InvalidOperationException("a bug, not a full disk")));
    }

    /// <summary>
    /// <c>IAppSettingsService</c> carries an explicit warning that state nobody reads is state the owner is
    /// never told about — that is how F-DURA-01 stayed invisible from v4.99.4. This pins that the new
    /// failure signal actually has a consumer.
    /// </summary>
    [Fact]
    public void TheShellSubscribesToTheSaveFailure()
    {
        var mainWindow = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "MainWindow.xaml.cs"));

        Assert.Contains("AppSettings.SaveFailed += OnSettingsSaveFailed", mainWindow, StringComparison.Ordinal);
        Assert.Contains("AppSettings.SaveFailed -= OnSettingsSaveFailed", mainWindow, StringComparison.Ordinal);
        Assert.True(
            Regex.IsMatch(mainWindow, @"OnSettingsSaveFailed\s*\(object\?\s+sender,\s*string\s+reason\)"),
            "MainWindow subscribes to SaveFailed but has no handler for it.");
    }
}
