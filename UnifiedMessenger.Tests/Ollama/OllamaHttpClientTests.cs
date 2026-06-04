using System.Net;
using System.Text;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests.Ollama;

public class OllamaHttpClientTests
{
    [Fact]
    public async Task TryPingAsync_ReturnsTrueWhenTagsEndpointSucceeds()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        using var client = new OllamaHttpClient(http);

        Assert.True(await client.TryPingAsync());
    }

    [Fact]
    public async Task ListModelNamesAsync_ParsesTagsResponse()
    {
        const string json = """
            {
              "models": [
                { "name": "phi3:mini", "model": "phi3:mini" },
                { "name": "llama3.2:latest", "model": "llama3.2:latest" }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        using var client = new OllamaHttpClient(http);

        var models = await client.ListModelNamesAsync();

        Assert.Equal(2, models.Count);
        Assert.Contains("phi3:mini", models);
    }

    [Fact]
    public async Task StreamGenerateAsync_YieldsTokenChunksWithoutBufferingFullResponse()
    {
        const string payload = """
            {"model":"phi3:mini","response":"Hel","done":false}
            {"model":"phi3:mini","response":"lo","done":true}
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        using var client = new OllamaHttpClient(http);

        var tokens = new List<string>();
        await foreach (var token in client.StreamGenerateAsync("phi3:mini", "Hi", null))
        {
            tokens.Add(token);
        }

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Hello", string.Concat(tokens));
    }

    [Fact]
    public async Task StreamPullAsync_ParsesProgressAndCompletion()
    {
        const string payload = """
            {"status":"pulling manifest","completed":0,"total":0}
            {"status":"downloading","completed":50,"total":100}
            {"status":"success","completed":100,"total":100}
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        using var client = new OllamaHttpClient(http);

        var updates = new List<UnifiedMessenger.Models.Ollama.OllamaPullProgress>();
        await foreach (var update in client.StreamPullAsync("phi3:mini"))
        {
            updates.Add(update);
        }

        Assert.True(updates.Count >= 2);
        Assert.Equal(50, updates[^2].PercentComplete, 1);
        Assert.True(updates[^1].IsComplete);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
