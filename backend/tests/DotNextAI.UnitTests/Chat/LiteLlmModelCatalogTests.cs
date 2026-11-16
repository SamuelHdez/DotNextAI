using System.Net;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;

namespace DotNextAI.UnitTests.Chat;

public class LiteLlmModelCatalogTests
{
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    // GET /models of the ERNI LiteLLM proxy, 2026-11-16.
    private const string ProxyModels = """
        {"data":[
          {"id":"gpt-5.6-terra","object":"model","created":1677610602,"owned_by":"openai","mode":"chat","max_input_tokens":922000,"max_output_tokens":128000},
          {"id":"gpt-5.6-luna","object":"model","created":1677610602,"owned_by":"openai","mode":"chat","max_input_tokens":922000,"max_output_tokens":128000},
          {"id":"gpt-realtime-1.5","object":"model","created":1677610602,"owned_by":"openai","mode":"realtime","max_input_tokens":32000,"max_output_tokens":4096},
          {"id":"text-embedding-3-small","object":"model","created":1677610602,"owned_by":"openai","mode":"embedding","max_input_tokens":8192},
          {"id":"gpt-image-2","object":"model","created":1677610602,"owned_by":"openai","mode":"image_generation"}
        ],"object":"list"}
        """;

    private static LiteLlmModelCatalog CreateCatalog(StubHttpHandler handler, TimeProvider? time = null, string? endpoint = "https://litellm.example.com/", string defaultModel = "gpt-5.6-luna") =>
        new(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions { Endpoint = endpoint, ApiKey = ApiKey, DefaultModel = defaultModel, TimeoutSeconds = 30 }),
            new StubHttpClientFactory(handler),
            time ?? TimeProvider.System);

    [Fact]
    public async Task GetChatModelsAsync_ProxyList_ReturnsOnlyChatModelsWithTheDefaultFirst()
    {
        using var catalog = CreateCatalog(StubHttpHandler.Json(HttpStatusCode.OK, ProxyModels));

        var models = await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ChatModel("gpt-5.6-luna", IsDefault: true, 922_000, 128_000),
                new ChatModel("gpt-5.6-terra", IsDefault: false, 922_000, 128_000),
            ],
            models);
        Assert.Equal("gpt-5.6-luna", catalog.DefaultModel);
    }

    [Fact]
    public async Task GetChatModelsAsync_CallsTheModelsRouteOfTheEndpointWithTheKey()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ProxyModels);
        using var catalog = CreateCatalog(handler, endpoint: "https://litellm.example.com/v1/");

        await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://litellm.example.com/v1/models"), request.Uri);
        Assert.Equal($"Bearer {ApiKey}", request.Authorization);
    }

    [Fact]
    public async Task GetChatModelsAsync_DefaultMissingFromTheList_IsAddedFirst()
    {
        using var catalog = CreateCatalog(StubHttpHandler.Json(HttpStatusCode.OK, ProxyModels), defaultModel: "private-model");

        var models = await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ChatModel("private-model", IsDefault: true), models[0]);
        Assert.Equal(3, models.Count);
    }

    [Fact]
    public async Task GetChatModelsAsync_PlainOpenAiListWithoutMode_KeepsEveryModel()
    {
        using var catalog = CreateCatalog(StubHttpHandler.Json(HttpStatusCode.OK, """{"data":[{"id":"gpt-5.6-luna"},{"id":"other"},{"id":"other"},{"object":"model"}]}"""));

        var models = await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["gpt-5.6-luna", "other"], models.Select(model => model.Id));
        Assert.Null(models[0].MaxInputTokens);
    }

    [Fact]
    public async Task GetChatModelsAsync_IsCachedForFiveMinutes()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ProxyModels);
        var time = new ManualTimeProvider();
        using var catalog = CreateCatalog(handler, time);

        await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(4));
        await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);
        Assert.Single(handler.Requests);

        time.Advance(TimeSpan.FromMinutes(2));
        await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetChatModelsAsync_FailuresAreNotCached()
    {
        var calls = 0;
        var handler = new StubHttpHandler(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ProxyModels) });
        using var catalog = CreateCatalog(handler);

        await Assert.ThrowsAsync<ChatServiceException>(() => catalog.GetChatModelsAsync(TestContext.Current.CancellationToken));
        var models = await catalog.GetChatModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, models.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ChatFailure.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, ChatFailure.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ChatFailure.Unavailable)]
    public async Task GetChatModelsAsync_ErrorStatus_MapsToFailure(HttpStatusCode status, ChatFailure failure)
    {
        using var catalog = CreateCatalog(StubHttpHandler.Json(status, """{"error":{"message":"nope"}}"""));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => catalog.GetChatModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(failure, ex.Failure);
    }

    [Fact]
    public async Task GetChatModelsAsync_InvalidJson_ThrowsUnavailable()
    {
        using var catalog = CreateCatalog(StubHttpHandler.Json(HttpStatusCode.OK, "<html>"));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => catalog.GetChatModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.Unavailable, ex.Failure);
    }

    [Fact]
    public async Task GetChatModelsAsync_NetworkError_ThrowsUnavailable()
    {
        using var catalog = CreateCatalog(StubHttpHandler.Throws(new HttpRequestException("No such host.")));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => catalog.GetChatModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.Unavailable, ex.Failure);
    }

    [Fact]
    public async Task GetChatModelsAsync_NotConfigured_ThrowsWithoutHttp()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, ProxyModels);
        using var catalog = CreateCatalog(handler, endpoint: null);

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => catalog.GetChatModelsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.NotConfigured, ex.Failure);
        Assert.Null(catalog.DefaultModel);
        Assert.Empty(handler.Requests);
    }
}
