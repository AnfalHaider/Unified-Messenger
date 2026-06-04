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
    }

    [Fact]
    public void ConnectionHandshakeScript_IncludesGoogleBusinessSelectors()
    {
        var script = ReadScript("connection-handshake.js");

        Assert.Contains("googlebusiness", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Business Profile", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        return File.ReadAllText(path);
    }
}
