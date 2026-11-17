using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Chat;

/// <summary>Token metrics over HTTP (step 10).</summary>
public class UsageEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    private static FakeChatService Counting() =>
        new(_ => new ChatAnswer("Hello Ana!", "gpt-5.6-luna", false, new TokenUsage(120, 4, 124, 0.0000141m)));

    [Fact]
    public async Task Chat_ReturnsTheUsage()
    {
        using var client = factory.CreateClientWith(Counting());

        using var response = await client.PostAsJsonAsync(new Uri("/api/chat", UriKind.Relative), new { question = "Hi" }, TestContext.Current.CancellationToken);

        var usage = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("usage");
        Assert.Equal(120, usage.GetProperty("inputTokens").GetInt64());
        Assert.Equal(4, usage.GetProperty("outputTokens").GetInt64());
        Assert.Equal(124, usage.GetProperty("totalTokens").GetInt64());
        Assert.Equal(0.0000141m, usage.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Chat_ModelWithoutAPrice_ReturnsANullCost()
    {
        using var client = factory.CreateClientWith(new FakeChatService(_ => new ChatAnswer("Hi", "gpt-5.6-luna", false, new TokenUsage(1, 1, 2))));

        using var response = await client.PostAsJsonAsync(new Uri("/api/chat", UriKind.Relative), new { question = "Hi" }, TestContext.Current.CancellationToken);

        var usage = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("usage");
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("cost").ValueKind);
    }

    [Fact]
    public async Task Chat_WithoutUsage_ReturnsNull()
    {
        using var client = factory.CreateClientWith(new FakeChatService());

        using var response = await client.PostAsJsonAsync(new Uri("/api/chat", UriKind.Relative), new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(JsonValueKind.Null, (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("usage").ValueKind);
    }

    [Fact]
    public async Task Stream_DoneEventCarriesTheUsage()
    {
        using var client = factory.CreateClientWith(Counting());

        using var response = await client.PostAsJsonAsync(new Uri("/api/chat/stream", UriKind.Relative), new { question = "Hi" }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var done = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n").Single(block => block.StartsWith("event: done", StringComparison.Ordinal));
        using var data = JsonDocument.Parse(done.Split("data: ", 2)[1]);
        Assert.Equal(124, data.RootElement.GetProperty("usage").GetProperty("totalTokens").GetInt64());
        Assert.Equal(0.0000141m, data.RootElement.GetProperty("usage").GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Images_ReturnTheUsage()
    {
        var images = new FakeImageService
        {
            Result = new GeneratedImage(new ChatImage(ImageFormats.Png, FakeImageService.Png), "gpt-image-2", null, new TokenUsage(12, 4160, 4172)),
        };
        using var client = factory.CreateClientWith(new FakeChatService(), images: images);

        using var response = await client.PostAsJsonAsync(new Uri("/api/images", UriKind.Relative), new { prompt = "x" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4172, (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("usage").GetProperty("totalTokens").GetInt64());
    }
}
