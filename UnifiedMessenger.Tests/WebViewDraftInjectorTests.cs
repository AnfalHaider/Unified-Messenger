using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewDraftInjectorTests
{
    [Theory]
    [InlineData("{\"ok\":true,\"reason\":\"filled\"}", true)]
    [InlineData("{\"ok\":false,\"reason\":\"compose-not-found\"}", false)]
    [InlineData("true", true)]
    public void ParseInjectResult_ReadsOkFlag(string raw, bool expected) =>
        Assert.Equal(expected, WebViewDraftInjector.ParseInjectResult(raw));
}
