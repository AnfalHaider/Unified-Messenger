using System.Diagnostics;
using Xunit;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Parses every script the app injects into a WebView.
/// </summary>
/// <remarks>
/// <para>This exists because of a real escape. The A3 selector migration left an unbalanced brace in
/// <c>whatsapp-adapter.js</c>. The whole 76 KB file therefore threw on load, every one of its globals was
/// undefined, and WhatsApp scraping was completely dead — and the full suite went green, published, and
/// installed without a word.</para>
/// <para>It went green because every other JS test in this project asserts on the script's <i>text</i>
/// (<c>Assert.Contains("__umConfig", script)</c>), which a syntactically broken file passes just as
/// happily as a working one. Nothing here executes the scripts, so nothing here parses them.</para>
/// <para>The failure was caught by driving the live app over CDP and noticing the adapter's globals were
/// missing. That check is manual and easy to skip. This one is not.</para>
/// </remarks>
public class InjectedScriptSyntaxTests
{
    private static string ScriptsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts");

    public static TheoryData<string> InjectedScripts()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(ScriptsDirectory, "*.js"))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InjectedScripts))]
    public void EveryInjectedScriptParses(string fileName)
    {
        var path = Path.Combine(ScriptsDirectory, fileName);
        Assert.True(File.Exists(path), $"Missing injected script: {path}");

        var (exitCode, stderr) = RunNodeCheck(path);

        Assert.True(
            exitCode == 0,
            $"{fileName} is not valid JavaScript and would throw on injection, taking every global it "
            + $"defines with it:{Environment.NewLine}{stderr}");
    }

    [Fact]
    public void TheScriptsDirectoryIsNotEmpty()
    {
        // A wrong directory would make the Theory above vacuous — zero cases, green, proving nothing.
        Assert.NotEmpty(Directory.GetFiles(ScriptsDirectory, "*.js"));
    }

    private static (int ExitCode, string Stderr) RunNodeCheck(string scriptPath)
    {
        var psi = new ProcessStartInfo("node", $"--check \"{scriptPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            // Deliberately a FAILURE, not a silent skip. A test that quietly passes when its only tool is
            // missing is the "green without exercising the thing it names" trap this repo has already been
            // bitten by; it would have let the A3 break through a second time.
            throw new InvalidOperationException(
                "Node is required to parse the injected scripts and was not found on PATH. "
                + "Install Node (CI runners have it) — this check is what stands between a broken "
                + $"scraper script and a customer. Underlying error: {ex.Message}",
                ex);
        }

        Assert.NotNull(process);
        var stderr = process!.StandardError.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, stderr);
    }
}
