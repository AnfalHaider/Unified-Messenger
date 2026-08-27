using System.IO.Compression;

namespace UnifiedMessenger.Services;

/// <summary>
/// One-click local backup / restore of the app's data stores — a safety net that keeps everything on the
/// machine (nothing is uploaded anywhere). Backs up the small JSON stores (settings, instances, analytics,
/// response times, KPI trend, oversight snapshot, overrides, triage) plus custom account avatars.
///
/// Deliberately EXCLUDES the WebView2 profile folder (huge, machine-bound signed-in sessions) and the Ollama
/// runtime/models (multi-GB, re-downloadable) — restoring those elsewhere wouldn't work anyway. Restore
/// overwrites the current stores in place; the caller should prompt for a restart so services reload them.
/// </summary>
public sealed class LocalBackupService
{
    private static readonly Lazy<LocalBackupService> LazyInstance = new(() => new LocalBackupService());

    public static LocalBackupService Instance => LazyInstance.Value;

    // Root subdirectories that are intentionally not backed up (too large / not portable).
    private static readonly HashSet<string> ExcludedDirectories =
        new(StringComparer.OrdinalIgnoreCase) { "WebView2", "ollama", "logs" };

    // A restore archive must contain at least one of these to be recognised as a genuine backup.
    private static readonly string[] SignatureFiles = ["settings.json", "instances.json"];

    private readonly string _userDataRoot;

    private LocalBackupService()
        : this(ApplicationPaths.UserDataRoot)
    {
    }

    internal LocalBackupService(string userDataRoot)
    {
        _userDataRoot = userDataRoot;
    }

    /// <summary>Files/folders that would be included in a backup, for a pre-backup summary.</summary>
    public int CountBackupEntries() => EnumerateBackupEntries().Count;

    /// <summary>
    /// Writes a zip of the data stores to <paramref name="destinationZipPath"/> (overwriting). Returns the
    /// number of files written. Runs off the UI thread.
    /// </summary>
    public async Task<int> CreateBackupAsync(string destinationZipPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationZipPath))
        {
            throw new ArgumentException("Destination path is required.", nameof(destinationZipPath));
        }

        // Write the live stores to disk BEFORE zipping them. Several of them debounce their saves by
        // design, so without this a backup taken right after a working session silently omitted that
        // session — the triage decisions and KPI trend points the owner had just made were still in memory
        // and never reached the archive. A backup that quietly excludes your most recent work is worse
        // than no backup, because you only discover it when you need it.
        await ApplicationLifecycleService.FlushPersistentStateAsync(cancellationToken).ConfigureAwait(false);

        if (ApplicationLifecycleService.LastFlushFailures.Count > 0)
        {
            throw new IOException(
                "Some of your data could not be written to disk, so a backup taken now would be incomplete: "
                + string.Join(", ", ApplicationLifecycleService.LastFlushFailures)
                + ". Close and reopen Unified Messenger, then try again.");
        }

        return await Task.Run(() =>
        {
            var entries = EnumerateBackupEntries();

            // Write to a temp file first, then move into place, so a failure can't leave a half-written zip.
            var tempPath = destinationZipPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var (absolutePath, relativePath) in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(absolutePath, relativePath, CompressionLevel.Optimal);
                }
            }

            File.Move(tempPath, destinationZipPath, overwrite: true);
            return entries.Count;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True when the zip at <paramref name="zipPath"/> looks like a Unified Messenger backup.</summary>
    /// <remarks>
    /// Only a genuine "this is not one of our archives" answers false. A bare <c>catch</c> here used to
    /// swallow <see cref="IOException"/> (file held by OneDrive or antivirus) and
    /// <see cref="UnauthorizedAccessException"/> as well, so the caller told the owner their perfectly good
    /// backup "isn't a recognised Unified Messenger backup" — and they might reasonably delete it. Those
    /// two now propagate, carrying a message that says what actually went wrong.
    /// </remarks>
    public bool IsRecognisedBackup(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries.Any(entry =>
                SignatureFiles.Contains(entry.FullName, StringComparer.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false; // not a zip at all, or a corrupt one
        }
        catch (IOException ex)
        {
            throw new IOException(
                "That file could not be opened — it may be in use by another program, or still syncing.",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                "Unified Messenger does not have permission to read that file.",
                ex);
        }
    }

    /// <summary>
    /// Extracts a backup zip into the user-data root, overwriting existing stores. Skips any entry that would
    /// escape the root (zip-slip) or that targets an excluded directory. Returns the number of files restored.
    /// The caller must restart the app so services reload the restored data.
    /// </summary>
    public async Task<int> RestoreAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        if (!IsRecognisedBackup(zipPath))
        {
            throw new InvalidDataException("This file isn't a recognised Unified Messenger backup.");
        }

        return await Task.Run(() =>
        {
            Directory.CreateDirectory(_userDataRoot);
            var rootFull = Path.GetFullPath(_userDataRoot) + Path.DirectorySeparatorChar;
            var restored = 0;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // directory marker
                }

                var topSegment = entry.FullName.Replace('\\', '/').Split('/')[0];
                if (ExcludedDirectories.Contains(topSegment))
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(_userDataRoot, entry.FullName));
                if (!destination.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // zip-slip guard
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
                restored++;
            }

            return restored;
        }, cancellationToken).ConfigureAwait(false);
    }

    // Root-level *.json stores plus the avatars folder (recursively). Relative paths use forward slashes.
    private List<(string AbsolutePath, string RelativePath)> EnumerateBackupEntries()
    {
        var entries = new List<(string, string)>();
        if (!Directory.Exists(_userDataRoot))
        {
            return entries;
        }

        foreach (var file in Directory.EnumerateFiles(_userDataRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            entries.Add((file, Path.GetFileName(file)));
        }

        var avatarsDir = Path.Combine(_userDataRoot, "avatars");
        if (Directory.Exists(avatarsDir))
        {
            foreach (var file in Directory.EnumerateFiles(avatarsDir, "*", SearchOption.AllDirectories))
            {
                var relative = "avatars/" + Path.GetRelativePath(avatarsDir, file).Replace('\\', '/');
                entries.Add((file, relative));
            }
        }

        return entries;
    }
}
