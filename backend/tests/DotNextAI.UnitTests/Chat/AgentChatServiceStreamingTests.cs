using System.Net;
using System.Text;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using DotNextAI.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNextAI.UnitTests.Chat;

public class AgentChatServiceStreamingTests
{
    private static readonly string ApiKey = $"sk-{Guid.NewGuid():N}";

    private const string Model = "gpt-5.6-luna";

    private static string Chunk(string? content, string? finishReason = null, string model = Model) =>
        "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-stream",
            @object = "chat.completion.chunk",
            created = 1_700_000_000,
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = content is null ? (object)new { } : new { role = "assistant", content },
                    finish_reason = finishReason,
                },
            },
        }) + "\n\n";

    private static string Stream(params string[] pieces) =>
        string.Concat(pieces.Select(piece => Chunk(piece))) + Chunk(null, "stop") + "data: [DONE]\n\n";

    private static AgentChatService CreateService(StubHttpHandler handler, IModelPrices? prices = null) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new LlmOptions
            {
                Endpoint = "https://litellm.example.com/",
                ApiKey = ApiKey,
                DefaultModel = Model,
                TimeoutSeconds = 30,
                MaxRetries = 0,
            }),
            new StubHttpClientFactory(handler),
            NullLoggerFactory.Instance,
            new Cli.FakeModelCatalog(),
            prices);

    private static async Task<List<ChatStreamUpdate>> CollectAsync(IAsyncEnumerable<ChatStreamUpdate> updates)
    {
        var all = new List<ChatStreamUpdate>();
        await foreach (var update in updates)
        {
            all.Add(update);
        }

        return all;
    }

    [Fact]
    public async Task AskStreamingAsync_RelaysThePiecesThenTheWholeAnswer()
    {
        var handler = StubHttpHandler.Sse(Stream("Hel", "lo ", "Ana!"));
        var service = CreateService(handler);

        var updates = await CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken));

        Assert.Equal(["Hel", "lo ", "Ana!"], updates.Where(u => u.Completed is null).Select(u => u.Text));
        var answer = Assert.Single(updates, u => u.Completed is not null).Completed!;
        Assert.Same(updates[^1].Completed, answer);
        Assert.Equal("Hello Ana!", answer.Text);
        Assert.Equal(Model, answer.Model);
        Assert.False(answer.Truncated);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task AskStreamingAsync_UsageInTheFinalChunk_IsOnTheWholeAnswer()
    {
        // With stream_options.include_usage the API ends with a chunk that has no choices and the usage.
        var usageChunk = "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-stream",
            @object = "chat.completion.chunk",
            created = 1_700_000_000,
            model = Model,
            choices = Array.Empty<object>(),
            usage = new { prompt_tokens = 42, completion_tokens = 7, total_tokens = 49 },
        }) + "\n\n";
        var service = CreateService(StubHttpHandler.Sse(Chunk("Hi") + Chunk(null, "stop") + usageChunk + "data: [DONE]\n\n"));

        var updates = await CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken));

        Assert.Equal(new TokenUsage(42, 7, 49), updates[^1].Completed!.Usage);
    }

    [Fact]
    public async Task AskStreamingAsync_ModelWithAPrice_PutsTheCostOnTheWholeAnswer()
    {
        var usageChunk = "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-stream",
            @object = "chat.completion.chunk",
            created = 1_700_000_000,
            model = "azure/gpt-5.6-luna-2026",
            choices = Array.Empty<object>(),
            usage = new { prompt_tokens = 42, completion_tokens = 7, total_tokens = 49 },
        }) + "\n\n";
        var prices = new FakeModelPrices(new Dictionary<string, ModelPrice> { [Model] = new(0.000001m, 0.000004m) });
        var service = CreateService(StubHttpHandler.Sse(Chunk("Hi") + Chunk(null, "stop") + usageChunk + "data: [DONE]\n\n"), prices);

        var updates = await CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken));

        Assert.Equal((42 * 0.000001m) + (7 * 0.000004m), updates[^1].Completed!.Usage!.Cost);
        Assert.Equal([Model], prices.Asked);
    }

    [Fact]
    public async Task AskStreamingAsync_NoUsageChunk_ReportsNone()
    {
        var service = CreateService(StubHttpHandler.Sse(Stream("Hi")));

        var updates = await CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken));

        Assert.Null(updates[^1].Completed!.Usage);
    }

    [Fact]
    public async Task AskStreamingAsync_FinishReasonLength_MarksTheAnswerAsTruncated()
    {
        var service = CreateService(StubHttpHandler.Sse(Chunk("A long") + Chunk(null, "length") + "data: [DONE]\n\n"));

        var updates = await CollectAsync(service.AskStreamingAsync("Tell me everything", [], [], TestContext.Current.CancellationToken));

        Assert.True(updates[^1].Completed!.Truncated);
    }

    [Fact]
    public async Task AskStreamingAsync_ProxyRefusesBeforeStreaming_ThrowsTheMappedFailureOnTheFirstMove()
    {
        var body = JsonSerializer.Serialize(new { error = new { message = $"litellm.BadRequestError: There are no healthy deployments for this model" } });
        var service = CreateService(StubHttpHandler.Json(HttpStatusCode.BadRequest, body));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken)));

        Assert.Equal(ChatFailure.ModelUnavailable, ex.Failure);
    }

    [Fact]
    public async Task AskStreamingAsync_ConnectionBreaksMidAnswer_ThrowsUnavailableAfterThePiecesSoFar()
    {
        var service = CreateService(StubHttpHandler.SseThatBreaks(Chunk("Hel")));
        var pieces = new List<string>();

        var ex = await Assert.ThrowsAsync<ChatServiceException>(async () =>
        {
            await foreach (var update in service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken))
            {
                pieces.Add(update.Text);
            }
        });

        Assert.Equal(ChatFailure.Unavailable, ex.Failure);
        Assert.Equal("The connection to the LiteLLM proxy broke before the answer was complete.", ex.Message);
        Assert.Equal(["Hel"], pieces);
    }

    [Fact]
    public async Task AskStreamingAsync_NoText_ThrowsEmptyAnswer()
    {
        var service = CreateService(StubHttpHandler.Sse(Chunk(null, "stop") + "data: [DONE]\n\n"));

        var ex = await Assert.ThrowsAsync<ChatServiceException>(
            () => CollectAsync(service.AskStreamingAsync("Hi", [], [], TestContext.Current.CancellationToken)));

        Assert.Equal(ChatFailure.EmptyAnswer, ex.Failure);
    }

    [Fact]
    public async Task AskStreamingAsync_InvalidQuestion_ThrowsArgumentExceptionWithoutCallingTheProxy()
    {
        var handler = StubHttpHandler.Sse(Stream("ok"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CollectAsync(service.AskStreamingAsync("  ", [], [], TestContext.Current.CancellationToken)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AskStreamingAsync_SendsHistoryAndImagesLikeTheNonStreamingCall()
    {
        var handler = StubHttpHandler.Sse(Stream("Blue."));
        var service = CreateService(handler);
        ChatTurn[] history = [new(ChatTurnRole.User, "Describe it"), new(ChatTurnRole.Assistant, "A square.")];

        await CollectAsync(service.AskStreamingAsync(
            "Colour?", history, [new ChatImage(ImageFormats.Png, TestImages.Png)], TestContext.Current.CancellationToken));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(4, messages.Length);
        Assert.Equal("image_url", messages[3].GetProperty("content")[1].GetProperty("type").GetString());
    }
}
