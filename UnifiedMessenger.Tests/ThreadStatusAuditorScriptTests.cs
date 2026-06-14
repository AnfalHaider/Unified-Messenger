namespace UnifiedMessenger.Tests;

public class ThreadStatusAuditorScriptTests
{
    private static string ReadThreadStatusAuditorScript()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "thread-status-auditor.js");
        Assert.True(File.Exists(scriptPath), $"Missing thread status auditor script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }

    [Fact]
    public void ThreadStatusAuditorScript_ExistsInOutput()
    {
        ReadThreadStatusAuditorScript();
    }

    [Fact]
    public void ThreadStatusAuditorScript_SupportsWhatsAppProfilesOnly()
    {
        var script = ReadThreadStatusAuditorScript();

        Assert.Contains("whatsapp:", script, StringComparison.Ordinal);
        Assert.Contains("whatsappbusiness:", script, StringComparison.Ordinal);
        Assert.Contains("isWhatsAppOutgoing", script, StringComparison.Ordinal);
        Assert.DoesNotContain("metabusiness", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googlebusiness", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isMetaOutgoing", script, StringComparison.Ordinal);
        Assert.DoesNotContain("isGoogleOwnerReply", script, StringComparison.Ordinal);
    }
}
