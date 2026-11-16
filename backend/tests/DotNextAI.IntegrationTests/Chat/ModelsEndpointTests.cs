using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Chat;

public class ModelsEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetModels_ReturnsTheDefaultAndTheChatModels()
    {
        using var client = factory.CreateClientWith(new FakeChatService());

        using var response = await client.GetAsync(new Uri("/api/models", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("gpt-5.6-luna", body.GetProperty("defaultModel").GetString());
        var models = body.GetProperty("models").EnumerateArray().ToArray();
        Assert.Equal(["gpt-5.6-luna", "gpt-5.6-terra"], models.Select(m => m.GetProperty("id").GetString()));
        Assert.True(models[0].GetProperty("isDefault").GetBoolean());
        Assert.Equal(922_000, models[1].GetProperty("maxInputTokens").GetInt32());
        Assert.Equal(128_000, models[1].GetProperty("maxOutputTokens").GetInt32());
    }

    [Fact]
    public async Task GetModels_NotConfigured_Returns503()
    {
        using var client = factory.CreateClientWith(new FakeChatService(), catalog: new FakeModelCatalog { DefaultModel = null });

        using var response = await client.GetAsync(new Uri("/api/models", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("NotConfigured", (await ReadJsonAsync(response)).GetProperty("failure").GetString());
    }

    [Fact]
    public async Task GetModels_ProxyFailure_ReturnsTheProblem()
    {
        var catalog = new FakeModelCatalog { Failure = new ChatServiceException(ChatFailure.Unauthorized, "The LiteLLM proxy rejected the API key.") };
        using var client = factory.CreateClientWith(new FakeChatService(), catalog: catalog);

        using var response = await client.GetAsync(new Uri("/api/models", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Unauthorized", (await ReadJsonAsync(response)).GetProperty("failure").GetString());
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    public async Task Post_WithAChatModel_PassesIt(string path)
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(path, new { question = "Hi", model = " gpt-5.6-terra " }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("gpt-5.6-terra", Assert.Single(chat.Calls).Model);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    public async Task Post_WithoutModelOrWithTheDefault_NeedsNoListLookup(string path)
    {
        var chat = new FakeChatService();
        var catalog = new FakeModelCatalog();
        using var client = factory.CreateClientWith(chat, catalog: catalog);

        using var first = await client.PostAsJsonAsync(path, new { question = "Hi" }, TestContext.Current.CancellationToken);
        using var second = await client.PostAsJsonAsync(path, new { question = "Hi", model = "gpt-5.6-luna" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(0, catalog.Reads);
        Assert.Null(chat.Calls[0].Model);
    }

    [Theory]
    [InlineData("/api/chat", "gpt-image-2")]
    [InlineData("/api/chat/stream", "made-up-model")]
    public async Task Post_WithAModelThatIsNotForChat_Returns400WithoutCallingTheModel(string path, string model)
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(path, new { question = "Hi", model }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors").GetProperty("model");
        Assert.Contains($"'{model}' is not a chat model", errors[0].GetString(), StringComparison.Ordinal);
        Assert.Empty(chat.Calls);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    public async Task Post_WithAModel_WhenTheListFails_ReturnsTheProblem(string path)
    {
        var catalog = new FakeModelCatalog { Failure = new ChatServiceException(ChatFailure.Timeout, "Too slow.") };
        using var client = factory.CreateClientWith(new FakeChatService(), catalog: catalog);

        using var response = await client.PostAsJsonAsync(path, new { question = "Hi", model = "gpt-5.6-terra" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }
}
