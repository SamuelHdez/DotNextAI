using System.Net;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNextAI.UnitTests.Chat;

public class AgentChatServiceTests
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

    private static AgentChatService CreateService(
        StubHttpHandler handler,
        string? endpoint = "https://litellm.example.com/",
        string? apiKey = null,
        string? model = Model,
        Cli.FakeModelCatalog? catalog = null) =>
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
            NullLoggerFactory.Instance,
            catalog ?? new Cli.FakeModelCatalog());

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
    public async Task AskAsync_SendsTheAgentInstructionsAndTheQuestionToTheProxyWithTheKey()
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
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(AgentChatService.Instructions, ContentText(messages[0]));
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("What is .NET?", ContentText(messages[1]));
    }

    [Fact]
    public async Task AskAsync_IsStateless_EachQuestionIsSentWithoutPreviousTurns()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        await service.AskAsync("First question", TestContext.Current.CancellationToken);
        await service.AskAsync("Second question", TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        using var second = JsonDocument.Parse(handler.Requests[1].Body!);
        var messages = second.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);
        Assert.Equal("Second question", ContentText(messages[1]));
    }

    [Fact]
    public async Task AskAsync_WithHistory_SendsInstructionsThenTheTurnsInOrderThenTheQuestion()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("Ana"));
        var service = CreateService(handler);
        ChatTurn[] history =
        [
            new(ChatTurnRole.User, "My name is Ana."),
            new(ChatTurnRole.Assistant, "Nice to meet you, Ana."),
        ];

        await service.AskAsync("What is my name?", history, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        var messages = body.RootElement.GetProperty("messages").EnumerateArray()
            .Select(m => (Role: m.GetProperty("role").GetString(), Text: ContentText(m)))
            .ToArray();
        Assert.Equal(
            [
                ("system", AgentChatService.Instructions),
                ("user", "My name is Ana."),
                ("assistant", "Nice to meet you, Ana."),
                ("user", "What is my name?"),
            ],
            messages);
    }

    [Fact]
    public async Task AskAsync_HistoryOverTheLimit_ThrowsArgumentExceptionWithoutCallingTheProxy()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);
        var history = Enumerable.Range(0, ChatRules.MaxHistoryTurns + 1)
            .Select(i => new ChatTurn(ChatTurnRole.User, $"turn {i}"))
            .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AskAsync("ping", history, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AskAsync_WithImages_SendsThemAsBase64DataUrisAfterTheQuestionText()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("A small square."));
        var service = CreateService(handler);
        ChatImage[] images = [new(ImageFormats.Png, TestImages.Png), new(ImageFormats.Jpeg, TestImages.Jpeg)];

        var answer = await service.AskAsync("What is in these images?", [], images, TestContext.Current.CancellationToken);

        Assert.Equal("A small square.", answer.Text);
        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        var question = body.RootElement.GetProperty("messages").EnumerateArray().Last();
        Assert.Equal("user", question.GetProperty("role").GetString());
        var parts = question.GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal(3, parts.Length);
        Assert.Equal("text", parts[0].GetProperty("type").GetString());
        Assert.Equal("What is in these images?", parts[0].GetProperty("text").GetString());
        Assert.Equal("image_url", parts[1].GetProperty("type").GetString());
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(TestImages.Png)}", parts[1].GetProperty("image_url").GetProperty("url").GetString());
        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(TestImages.Jpeg)}", parts[2].GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task AskAsync_WithImages_KeepsTheHistoryAsText()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("Blue."));
        var service = CreateService(handler);
        ChatTurn[] history = [new(ChatTurnRole.User, "Describe it"), new(ChatTurnRole.Assistant, "A blue square.")];

        await service.AskAsync("And now this one?", history, [new ChatImage(ImageFormats.Png, TestImages.Png)], TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("Describe it", ContentText(messages[1]));
        Assert.Equal("A blue square.", ContentText(messages[2]));
    }

    [Fact]
    public async Task AskAsync_InvalidImage_ThrowsArgumentExceptionWithoutCallingTheProxy()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            "What is this?", [], [new ChatImage(ImageFormats.Png, TestImages.Text)], TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AskAsync_WithAnotherChatModel_SendsItToTheProxy()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong", model: "gpt-5.6-terra"));
        var catalog = new Cli.FakeModelCatalog();
        var service = CreateService(handler, catalog: catalog);

        var answer = await service.AskAsync(new ChatRequest("ping", [], [], "gpt-5.6-terra"), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.Equal("gpt-5.6-terra", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("gpt-5.6-terra", answer.Model);
        Assert.Equal(1, catalog.Reads);
    }

    [Fact]
    public async Task AskAsync_DefaultModel_NeedsNoCatalogLookup()
    {
        var catalog = new Cli.FakeModelCatalog();
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong")), catalog: catalog);

        await service.AskAsync(new ChatRequest("ping", [], [], Model), TestContext.Current.CancellationToken);
        await service.AskAsync("ping", TestContext.Current.CancellationToken);

        Assert.Equal(0, catalog.Reads);
    }

    [Fact]
    public async Task AskAsync_ModelThatIsNotAChatModel_ThrowsModelNotFoundWithoutCallingTheProxy()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => service.AskAsync(new ChatRequest("ping", [], [], "text-embedding-3-small"), TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.ModelNotFound, ex.Failure);
        Assert.Equal("'text-embedding-3-small' is not a chat model of the LiteLLM proxy.", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AskAsync_SwitchingModels_SendsEachQuestionToItsModel()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong"));
        var service = CreateService(handler);

        await service.AskAsync(new ChatRequest("one", [], [], "gpt-5.6-terra"), TestContext.Current.CancellationToken);
        await service.AskAsync(new ChatRequest("two", [], []), TestContext.Current.CancellationToken);
        await service.AskAsync(new ChatRequest("three", [], [], "gpt-5.6-terra"), TestContext.Current.CancellationToken);

        var models = handler.Requests.Select(request => JsonDocument.Parse(request.Body!).RootElement.GetProperty("model").GetString());
        Assert.Equal(["gpt-5.6-terra", Model, "gpt-5.6-terra"], models);
    }

    [Fact]
    public async Task AskAsync_ModelReportedByTheProxy_IsTheAnswerModel()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong", model: "gpt-5.6-luna-2026-10-01")));

        var answer = await service.AskAsync("ping", TestContext.Current.CancellationToken);

        Assert.Equal("gpt-5.6-luna-2026-10-01", answer.Model);
    }

    // The SDK may send content as a plain string or as an array of text parts.
    private static string? ContentText(JsonElement message)
    {
        var content = message.GetProperty("content");
        return content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : string.Concat(content.EnumerateArray().Select(part => part.GetProperty("text").GetString()));
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
    public async Task AskAsync_ReportsTheTokensOfTheCall()
    {
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, Completion("pong")));

        var answer = await service.AskAsync("ping", TestContext.Current.CancellationToken);

        Assert.Equal(new TokenUsage(5, 1, 6), answer.Usage);
    }

    [Fact]
    public async Task AskAsync_ProxyWithoutUsage_ReportsNone()
    {
        var body = JsonSerializer.Serialize(new
        {
            id = "x",
            @object = "chat.completion",
            created = 1,
            model = Model,
            choices = new[] { new { index = 0, message = new { role = "assistant", content = "pong" }, finish_reason = "stop" } },
        });
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.OK, body));

        var answer = await service.AskAsync("ping", TestContext.Current.CancellationToken);

        Assert.Null(answer.Usage);
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
    public async Task AskAsync_ModelNotAllowedForTheKey_ThrowsModelNotFoundNotUnauthorized()
    {
        // Real ERNI proxy answer for a model the key's team may not use (2026-11-16).
        var body = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = $"The requested model '{Model}' is not available for this API key, or the model name is invalid. Check the models available to you and try again.",
                type = "team_model_access_denied",
                param = "model",
                code = "403",
            },
        });
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.Forbidden, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(() => service.AskAsync("ping", TestContext.Current.CancellationToken));

        Assert.Equal(ChatFailure.ModelNotFound, ex.Failure);
        Assert.Equal($"The API key has no access to the model '{Model}' in the LiteLLM proxy.", ex.Message);
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
