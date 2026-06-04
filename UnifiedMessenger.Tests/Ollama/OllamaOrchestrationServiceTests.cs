using System.Net;
using System.Text;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests.Ollama;

[Collection("SettingsSerial")]
public class OllamaOrchestrationServiceTests : IAsyncLifetime
{
    private AppSettings _originalSettings = new();

    public Task InitializeAsync()
    {
        _originalSettings = CloneSettings(AppSettingsService.Instance.Settings);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await AppSettingsService.Instance.UpdateAsync(settings =>
        {
            settings.EnableLocalAi = _originalSettings.EnableLocalAi;
            settings.LocalAiModelName = _originalSettings.LocalAiModelName;
            settings.OllamaAutoBootstrap = _originalSettings.OllamaAutoBootstrap;
        });
    }

    [Fact]
    public async Task EnsureEngineRunningAsync_WhenLocalAiDisabled_ReturnsFalse()
    {
        await ApplySettingsAsync(enableLocalAi: false);

        using var handler = new StubHttpMessageHandler(_ => TagsOk());
        using var service = CreateService(handler);

        var running = await service.EnsureEngineRunningAsync();

        Assert.False(running);
        Assert.Equal(Models.Ollama.OllamaConnectionState.NotRunning, service.ConnectionState);
    }

    [Fact]
    public async Task EnsureEngineRunningAsync_WhenPingSucceeds_SetsRunning()
    {
        await ApplySettingsAsync(enableLocalAi: true);

        using var handler = new StubHttpMessageHandler(_ => TagsOk());
        using var service = CreateService(handler);

        Assert.True(await service.EnsureEngineRunningAsync());
        Assert.Equal(Models.Ollama.OllamaConnectionState.Running, service.ConnectionState);
    }

    [Fact]
    public async Task StreamGenerateAsync_RespectsSelectedModel()
    {
        await ApplySettingsAsync(enableLocalAi: true, modelName: "llama3.2:latest");

        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            const string payload = """
                {"model":"llama3.2:latest","response":"OK","done":true}
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var service = CreateService(handler);

        var output = new List<string>();
        await foreach (var token in service.StreamGenerateAsync("Draft a reply"))
        {
            output.Add(token);
        }

        Assert.Contains("llama3.2:latest", capturedBody, StringComparison.Ordinal);
        Assert.Equal("OK", string.Concat(output));
    }

    [Fact]
    public void ResolveModelName_UsesOverrideThenDefault()
    {
        Assert.Equal("custom", OllamaOrchestrationService.ResolveModelName(" custom "));
    }

    private static OllamaOrchestrationService CreateService(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        return new OllamaOrchestrationService(new OllamaHttpClient(http), new OllamaBootstrapService(new HttpClient(handler)));
    }

    private static async Task ApplySettingsAsync(bool enableLocalAi, string modelName = "phi3:mini") =>
        await AppSettingsService.Instance.UpdateAsync(settings =>
        {
            settings.EnableLocalAi = enableLocalAi;
            settings.LocalAiModelName = modelName;
            settings.OllamaAutoBootstrap = false;
        });

    private static AppSettings CloneSettings(AppSettings source) =>
        new()
        {
            EnableLocalAi = source.EnableLocalAi,
            LocalAiModelName = source.LocalAiModelName,
            OllamaAutoBootstrap = source.OllamaAutoBootstrap
        };

    private static HttpResponseMessage TagsOk() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json")
        };

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
