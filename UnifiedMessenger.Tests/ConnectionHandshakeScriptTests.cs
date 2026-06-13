namespace UnifiedMessenger.Tests;

public class ConnectionHandshakeScriptTests
{
    [Fact]
    public void ConnectionHandshakeScript_PostsConnectionStatusMessage()
    {
        var script = ReadScript("connection-handshake.js");

        Assert.Contains("type: 'connection-status'", script, StringComparison.Ordinal);
        Assert.Contains("__umStartConnectionHandshake", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("'Connected'", script, StringComparison.Ordinal);
        Assert.Contains("__umConnectionPollTimer", script, StringComparison.Ordinal);
        Assert.Contains("urlLoggedIn", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionHandshakeScript_IncludesWhatsAppSelectors()
    {
        var script = ReadScript("connection-handshake.js");

        Assert.Contains("whatsapp", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("web.whatsapp.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googlebusiness", script, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        return File.ReadAllText(path);
    }
}
