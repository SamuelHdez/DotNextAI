using System.Net;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.Infrastructure.Images;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNextAI.UnitTests.Images;

public class LiteLlmImageServiceTests
{
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    private static string Answer(byte[] image, string? revised = "A cat in a hat.") =>
        JsonSerializer.Serialize(new { created = 1, data = new[] { new { b64_json = Convert.ToBase64String(image), revised_prompt = revised } } });

    private static LiteLlmImageService CreateService(StubHttpHandler handler, string? imageModel = "gpt-image-2", string? endpoint = "https://litellm.example.com/") =>
        new(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions
            {
                Endpoint = endpoint,
                ApiKey = ApiKey,
                DefaultModel = "gpt-5.6-luna",
                ImageModel = imageModel,
                TimeoutSeconds = 30,
            }),
            new StubHttpClientFactory(handler),
            NullLogger<LiteLlmImageService>.Instance);

    [Fact]
    public async Task GenerateAsync_PostsThePromptToTheImagesRouteAndReturnsTheImage()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Answer(TestImages.Png));
        var service = CreateService(handler);

        var image = await service.GenerateAsync(new ImageRequest("  A cat in a hat  ", ImageSize.Landscape), TestContext.Current.CancellationToken);

        Assert.Equal(ImageFormats.Png, image.Image.MediaType);
        Assert.Equal(TestImages.Png, image.Image.Data.ToArray());
        Assert.Equal("gpt-image-2", image.Model);
        Assert.Equal("A cat in a hat.", image.RevisedPrompt);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://litellm.example.com/images/generations"), request.Uri);
        Assert.Equal($"Bearer {ApiKey}", request.Authorization);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("gpt-image-2", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("A cat in a hat", body.RootElement.GetProperty("prompt").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("n").GetInt32());
        Assert.Equal("1536x1024", body.RootElement.GetProperty("size").GetString());
        // gpt-image models reject response_format: they always answer base64.
        Assert.False(body.RootElement.TryGetProperty("response_format", out _));
    }

    [Theory]
    [InlineData(ImageSize.Square, "1024x1024")]
    [InlineData(ImageSize.Landscape, "1536x1024")]
    [InlineData(ImageSize.Portrait, "1024x1536")]
    public void SizeOf_MapsToTheGptImageSizes(ImageSize size, string expected)
    {
        Assert.Equal(expected, LiteLlmImageService.SizeOf(size));
    }

    [Fact]
    public async Task GenerateAsync_FormatComesFromTheBytes()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Answer(TestImages.Jpeg, revised: null)));

        var image = await service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken);

        Assert.Equal(ImageFormats.Jpeg, image.Image.MediaType);
        Assert.Null(image.RevisedPrompt);
    }

    [Theory]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"url":"https://example.com/cat.png"}]}""")]
    [InlineData("""{"data":[{"b64_json":"not base64!"}]}""")]
    [InlineData("""{"data":[{"b64_json":"VGhpcyBpcyBub3QgYW4gaW1hZ2Uu"}]}""")]
    public async Task GenerateAsync_AnswerWithoutAnImage_ThrowsEmptyAnswer(string body)
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.EmptyAnswer, ex.Failure);
        Assert.Equal("The model returned no image.", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_InvalidJson_ThrowsUnavailable()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, "<html>"));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.Unavailable, ex.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "litellm.BadRequestError: There are no healthy deployments for this model", null, ChatFailure.ModelUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, "Your request was rejected by the safety system.", null, ChatFailure.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden, "not available for this API key", "team_model_access_denied", ChatFailure.ModelNotFound)]
    [InlineData(HttpStatusCode.Unauthorized, "bad key", null, ChatFailure.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, "slow down", null, ChatFailure.RateLimited)]
    public async Task GenerateAsync_ProxyError_MapsLikeTheChat(HttpStatusCode status, string message, string? type, ChatFailure failure)
    {
        var body = JsonSerializer.Serialize(new { error = new { message, type } });
        var service = CreateService(StubHttpHandler.Json(status, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken));

        Assert.Equal(failure, ex.Failure);
    }

    [Fact]
    public async Task GenerateAsync_NoRetryOnServerError()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.BadGateway, "{}");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ChatServiceException>(() => service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_EmptyPrompt_ThrowsArgumentExceptionWithoutHttp(string prompt)
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Answer(TestImages.Png));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(new ImageRequest(prompt), TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateAsync_TooLongPrompt_ThrowsArgumentException()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Answer(TestImages.Png)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(
            new ImageRequest(new string('a', ImageRules.MaxPromptLength + 1)), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null, "https://litellm.example.com/")]
    [InlineData("gpt-image-2", null)]
    public async Task GenerateAsync_NotConfigured_ThrowsNotConfigured(string? imageModel, string? endpoint)
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Answer(TestImages.Png)), imageModel, endpoint);

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => service.GenerateAsync(new ImageRequest("x"), TestContext.Current.CancellationToken));

        Assert.False(service.IsConfigured);
        Assert.Null(service.Model);
        Assert.Equal(ChatFailure.NotConfigured, ex.Failure);
    }
}
