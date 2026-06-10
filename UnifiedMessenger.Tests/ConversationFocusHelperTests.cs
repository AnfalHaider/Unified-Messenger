using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class ConversationFocusHelperTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("\"true\"", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ParseScriptBoolean_ParsesWinUiScriptResults(string? raw, bool expected)
    {
        Assert.Equal(expected, ConversationFocusHelper.ParseScriptBoolean(raw));
    }
}
