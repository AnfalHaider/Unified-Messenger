using System.Text.RegularExpressions;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for T-03 and T-04.
///
/// <para>
/// Around twenty failure paths — every account operation in <c>ShellController</c>, every backup / restore /
/// import / export in Settings, the AI status lines, and <c>App.LaunchAsync</c> itself — showed the owner a
/// raw <c>ex.Message</c> and wrote nothing at all to <c>app.log</c>. So the owner saw a file path from inside
/// <c>%LOCALAPPDATA%</c>, and the one file support asks them to send said nothing about what had happened.
/// </para>
/// </summary>
public class UserFacingErrorTests
{
    private const string StorePath = @"C:\Users\owner\AppData\Local\UnifiedMessenger\instances.json";

    [Fact]
    public void AnAbsolutePathIsNeverShownToTheOwner()
    {
        var ex = new IOException(
            $"The process cannot access the file '{StorePath}' because it is being used by another process.");

        var shown = UserFacingError.Format(ex);

        Assert.DoesNotContain(StorePath, shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\", shown, StringComparison.Ordinal);
        Assert.Contains(UserFacingError.RedactedPath, shown, StringComparison.Ordinal);
        // The sentence must still read as a sentence, not as a stub.
        Assert.Contains("used by another process", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void AUncPathIsRedactedToo()
    {
        var shown = UserFacingError.Format(
            new IOException(@"Could not find a part of the path \\fileserver\profiles\owner\um\settings.json."));

        Assert.DoesNotContain(@"\\fileserver", shown, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The old formatter fell back to <c>ex.GetType().Name</c>, so an exception with no message showed the
    /// owner the word "COMException" at the moment the app was already failing them.
    /// </summary>
    [Fact]
    public void AnExceptionTypeNameIsNeverShownInsteadOfASentence()
    {
        var shown = UserFacingError.Format(new EmptyMessageException());

        Assert.DoesNotContain("Exception", shown, StringComparison.Ordinal);
        Assert.Equal(UserFacingError.NoDetail, shown);
    }

    [Fact]
    public void InnerDetailIsKeptBecauseItIsUsuallyTheRealCause()
    {
        var shown = UserFacingError.Format(
            new IOException("That file could not be opened.", new UnauthorizedAccessException("Access is denied.")));

        Assert.Contains("That file could not be opened.", shown, StringComparison.Ordinal);
        Assert.Contains("Access is denied.", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateFailuresAreListedOnceEach()
    {
        var shown = UserFacingError.Format(new AggregateException(
            new IOException("Access is denied."),
            new IOException("Access is denied."),
            new IOException("The disk is full.")));

        Assert.Equal(
            new[] { "Access is denied.", "The disk is full." },
            shown.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// One logged event stays one line, so <c>app.log</c> and a one-line dialog both stay legible. Several
    /// WinRT messages carry their own line breaks.
    /// </summary>
    [Fact]
    public void EmbeddedLineBreaksAreCollapsed()
    {
        var shown = UserFacingError.Format(
            new InvalidOperationException("A method was called at an unexpected time.\r\n\r\nNot applicable here."));

        Assert.Equal("A method was called at an unexpected time. Not applicable here.", shown);
    }

    [Fact]
    public void DescribeWritesTheFailureToTheLog()
    {
        // AppLogger.SuppressWritesForTests is set by the test assembly, so this asserts the contract rather
        // than the file: Describe must return the same text Format does, having logged on the way past.
        var ex = new IOException("Access is denied.");

        Assert.Equal(UserFacingError.Format(ex), UserFacingError.Describe("Test.Scope", ex));
    }

    /// <summary>
    /// Source-level guard, in the idiom of <c>AccountVocabularyTests</c>. The runtime tests above prove the
    /// formatter is right; this proves nothing bypasses it. A new dialog written next month with a raw
    /// <c>ex.Message</c> in it reintroduces both defects at once.
    /// </summary>
    [Fact]
    public void NoDialogShowsARawExceptionMessage()
    {
        var uiRoot = Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger");

        var offenders = Directory
            .EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(entry => Regex.IsMatch(
                    entry.Line,
                    @"(ShowErrorAsync|ShowMessageDialogAsync|ShowError)\s*\([^)]*\bex\.Message\b"))
                .Select(entry => $"{Path.GetFileName(path)}:{entry.Number}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These dialogs show a raw exception message, which leaks file paths to the owner and logs "
            + "nothing. Use UserFacingError.Describe(scope, ex): " + string.Join(", ", offenders));
    }

    private sealed class EmptyMessageException : Exception
    {
        public override string Message => string.Empty;
    }
}
