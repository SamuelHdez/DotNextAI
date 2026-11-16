using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Chat;

public class ImageEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    private static readonly Uri Images = new("/api/images", UriKind.Relative);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Post_ReturnsTheImageInBase64()
    {
        var images = new FakeImageService();
        using var client = factory.CreateClientWith(new FakeChatService(), images: images);

        using var response = await client.PostAsJsonAsync(Images, new { prompt = "  A pixel  ", size = "portrait" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("image/png", body.GetProperty("mediaType").GetString());
        Assert.Equal(FakeImageService.Png, Convert.FromBase64String(body.GetProperty("data").GetString()!));
        Assert.Equal("gpt-image-2", body.GetProperty("model").GetString());
        Assert.Equal("A tiny black pixel.", body.GetProperty("revisedPrompt").GetString());
        var request = Assert.Single(images.Requests);
        Assert.Equal("A pixel", request.Prompt);
        Assert.Equal(ImageSize.Portrait, request.Size);
    }

    [Fact]
    public async Task Post_WithoutSize_DrawsASquare()
    {
        var images = new FakeImageService();
        using var client = factory.CreateClientWith(new FakeChatService(), images: images);

        using var response = await client.PostAsJsonAsync(Images, new { prompt = "A pixel" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ImageSize.Square, Assert.Single(images.Requests).Size);
    }

    [Theory]
    [InlineData("""{ "prompt": " " }""")]
    [InlineData("""{ }""")]
    [InlineData("""{ "prompt": "x", "size": "huge" }""")]
    [InlineData("""{ "prompt": "x", "size": 1 }""")]
    public async Task Post_InvalidInput_Returns400WithoutGenerating(string json)
    {
        var images = new FakeImageService();
        using var client = factory.CreateClientWith(new FakeChatService(), images: images);

        using var response = await client.PostAsync(Images, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(images.Requests);
    }

    [Fact]
    public async Task Post_NotConfigured_Returns503()
    {
        using var client = factory.CreateClientWith(new FakeChatService(), images: new FakeImageService { IsConfigured = false });

        using var response = await client.PostAsJsonAsync(Images, new { prompt = "x" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("NotConfigured", (await ReadJsonAsync(response)).GetProperty("failure").GetString());
    }

    [Theory]
    [InlineData(ChatFailure.ModelUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(ChatFailure.BadRequest, HttpStatusCode.UnprocessableEntity)]
    [InlineData(ChatFailure.EmptyAnswer, HttpStatusCode.BadGateway)]
    public async Task Post_Failure_ReturnsTheProblem(ChatFailure failure, HttpStatusCode status)
    {
        var images = new FakeImageService { Failure = new ChatServiceException(failure, "Something failed.") };
        using var client = factory.CreateClientWith(new FakeChatService(), images: images);

        using var response = await client.PostAsJsonAsync(Images, new { prompt = "x" }, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(failure.ToString(), (await ReadJsonAsync(response)).GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Post_HasItsOwnRateLimit()
    {
        using var client = factory.CreateClientWith(new FakeChatService(), images: new FakeImageService(), imagePermitsPerMinute: 1);

        using var first = await client.PostAsJsonAsync(Images, new { prompt = "x" }, TestContext.Current.CancellationToken);
        using var second = await client.PostAsJsonAsync(Images, new { prompt = "x" }, TestContext.Current.CancellationToken);
        using var chat = await client.PostAsJsonAsync(new Uri("/api/chat", UriKind.Relative), new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
    }
}
