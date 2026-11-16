using System.Net;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNextAI.UnitTests.Chat;

public class OpenAiChatServiceTests
{
    // Generated per run: Semgrep (p/secrets) blocks literal keys.
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    private const string Model = "gpt-5.6-luna";

    private static string Completion(string? content, string finishReason = "stop", string? refusal = null, string model = Model) =>
        JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1_700_000_000,
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content, refusal },
                    finish_reason = finishReason,
                },
            },
            usage = new { prompt_tokens = 5, completion_tokens = 1, total_tokens = 6 },
        });

    private static string ProxyError(string message, string code = "400") =>
        JsonSerializer.Serialize(new { error = new { message, type = "invalid_request_error", param = (string?)null, code } });

    private static OpenAiChatService CreateService(
        StubHttpHandler handler,
        string? endpoint = "https://litellm.example.com/",
        string? apiKey = null,
        string? model = Model) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions
            {
                Endpoint = endpoint,
                ApiKey = apiKey ?? ApiKey,
                DefaultModel = model,
                TimeoutSeconds = 30,
                MaxRetries = 0,
            }),
            new StubHttpClientFactory(handler),
            NullLogger<OpenAiChatService>.Instance);

    [Fact]
    public async Task AskAsync_SuccessfulCompletion_ReturnsAnswerAndModel()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        var answer = await service.AskAsync("Reply with exactly: pong", TestContext.Current.CancellationToken);

        Assert.Equal("pong", answer.Text);
        Assert.Equal(Model, answer.Model);
        Assert.False(answer.Truncated);
    }

    [Fact]
    public async Task AskAsync_SendsOneUserMessageToTheProxyWithTheKey()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        await service.AskAsync("  What is .NET?  ", TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://litellm.example.com/chat/completions"), request.Uri);
        Assert.Equal($"Bearer {ApiKey}", request.Authorization);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(Model, body.RootElement.GetProperty("model").GetString());
        var message = Assert.Single(body.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("What is .NET?", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task AskAsync_EndpointWithPath_IsUsedAsBaseUrl()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler, endpoint: "https://litellm.example.com/v1/");

        await service.AskAsync("ping", TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://litellm.example.com/v1/chat/completions"), Assert.Single(handler.Requests).Uri);
    }

    [Fact]
    public async Task AskAsync_FinishReasonLength_MarksTheAnswerAsTruncated()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion("A long answer", finishReason: "length")));

        var answer = await service.AskAsync("Tell me everything", TestContext.Current.CancellationToken);

        Assert.True(answer.Truncated);
    }

    [Fact]
    public async Task AskAsync_Refusal_ReturnsTheRefusalText()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion(null, refusal: "I can't help with that.")));

        var answer = await service.AskAsync("Something not allowed", TestContext.Current.CancellationToken);

        Assert.Equal("I can't help with that.", answer.Text);
    }

    [Fact]
    public async Task AskAsync_EmptyContent_ThrowsEmptyAnswer()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion("   ")));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.EmptyAnswer, ex.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ChatFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ChatFailure.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, ChatFailure.ModelNotFound)]
    [InlineData(HttpStatusCode.BadRequest, ChatFailure.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests, ChatFailure.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ChatFailure.Unavailable)]
    [InlineData(HttpStatusCode.BadGateway, ChatFailure.Unavailable)]
    public async Task AskAsync_ErrorStatus_MapsToFailure(HttpStatusCode status, ChatFailure expected)
    {
        var service = CreateService(StubHttpHandler.Json(status, ProxyError("Something went wrong", ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture))));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Failure);
    }

    [Fact]
    public async Task AskAsync_NoHealthyDeployments_ThrowsModelUnavailable()
    {
        // Real LiteLLM answer when the model group has no healthy backend (seen on 2026-11-16).
        var body = ProxyError(
            $"litellm.BadRequestError: You passed in model={Model}. There are no healthy deployments for this model\n\nLiteLLM: model group '{Model}' failed with the error above. No fallback was attempted.");
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.BadRequest, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.ModelUnavailable, ex.Failure);
        Assert.Contains(Model, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_BadRequest_QuotesTheFirstLineOfTheProxyError()
    {
        var body = ProxyError("litellm.ContextWindowExceededError: too many tokens\nsecond line with internals");
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.BadRequest, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.BadRequest, ex.Failure);
        Assert.EndsWith("litellm.ContextWindowExceededError: too many tokens", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("second line", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_ProxyErrorEchoingTheKey_MasksIt()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.BadRequest, ProxyError($"Bad key {ApiKey} for this route")));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.DoesNotContain(ApiKey, ex.Message, StringComparison.Ordinal);
        Assert.Contains("***", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_Unauthorized_NeverQuotesTheProxyBody()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.Unauthorized, ProxyError($"Invalid key {ApiKey[..10]}...", "401")));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal("The LiteLLM proxy rejected the API key.", ex.Message);
    }

    [Fact]
    public async Task AskAsync_NonJsonErrorBody_FallsBackToAGenericMessage()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("<html>Bad gateway</html>"),
        });
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal("The LiteLLM proxy rejected the request.", ex.Message);
    }

    [Fact]
    public async Task AskAsync_NetworkError_ThrowsUnavailable()
    {
        var service = CreateService(StubHttpHandler.Throws(new HttpRequestException("No such host is known.")));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.Unavailable, ex.Failure);
    }

    [Fact]
    public async Task AskAsync_TimeoutWithoutCallerCancellation_ThrowsTimeout()
    {
        var service = CreateService(StubHttpHandler.Throws(new TaskCanceledException("The request timed out.", new TimeoutException())));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.Timeout, ex.Failure);
    }

    [Fact]
    public async Task AskAsync_CallerCancels_ThrowsOperationCanceled()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AskAsync("ping", cancelled.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://litellm.example.com/")]
    public async Task AskAsync_NotConfigured_ThrowsNotConfiguredWithoutCallingTheProxy(string? endpoint)
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler, endpoint: endpoint, model: endpoint is null ? Model : null);

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.False(service.IsConfigured);
        Assert.Equal(ChatFailure.NotConfigured, ex.Failure);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_EmptyQuestion_ThrowsArgumentExceptionWithoutCallingTheProxy(string question)
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(question, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }
}
