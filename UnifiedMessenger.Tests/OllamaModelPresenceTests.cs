using System.Net;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Telling "the AI runtime is not running" apart from "the model was never downloaded".
/// </summary>
/// <remarks>
/// Found live: Ollama was running with nothing pulled, so drafting a review reply failed with only "No draft
/// could be written" — true, and useless, because the owner cannot tell that one click in Settings → AI
/// would fix it. The runtime being up and the model being present are different things, and a feature that
/// conflates them is a dead end.
/// </remarks>
public class OllamaModelPresenceTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private static OllamaInferenceClient ClientReturning(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(() => "http://localhost:11434", new HttpClient(new StubHandler(status, body)));

    private const string TwoModels =
        """{"models":[{"name":"phi3:mini"},{"name":"llama3.1:8b"}]}""";

    [Fact]
    public async Task AnInstalledModelIsFound() =>
        Assert.True(await ClientReturning(TwoModels).IsModelInstalledAsync("phi3:mini"));

    [Fact]
    public async Task AModelThatWasNeverPulledIsReportedMissing() =>
        // The live case: Ollama up, model list empty.
        Assert.False(await ClientReturning("""{"models":[]}""").IsModelInstalledAsync("phi3:mini"));

    [Fact]
    public async Task AMissingModelAmongOthersIsReportedMissing() =>
        Assert.False(await ClientReturning(TwoModels).IsModelInstalledAsync("mistral:7b"));

    [Theory]
    [InlineData("phi3")]
    [InlineData("PHI3:MINI")]
    public async Task ATagWrittenLooselyStillMatches(string configured) =>
        // A user typing "phi3" into settings has the model; reporting it missing would send them to download
        // something already on disk.
        Assert.True(await ClientReturning(TwoModels).IsModelInstalledAsync(configured));

    [Fact]
    public async Task AnUnreadableModelListIsTreatedAsPresent()
    {
        // Degrade into the old behaviour rather than blocking a model that is really there: a changed API
        // shape must not make the feature claim the model is missing.
        Assert.True(await ClientReturning("not json at all").IsModelInstalledAsync("phi3:mini"));
        Assert.True(await ClientReturning("{}", HttpStatusCode.InternalServerError).IsModelInstalledAsync("phi3:mini"));
    }

    [Fact]
    public async Task NoModelNameIsNotAModel() =>
        Assert.False(await ClientReturning(TwoModels).IsModelInstalledAsync("  "));
}
