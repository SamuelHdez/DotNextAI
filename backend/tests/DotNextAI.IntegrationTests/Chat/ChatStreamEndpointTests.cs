using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.IntegrationTests.Infrastructure;

namespace DotNextAI.IntegrationTests.Chat;

public class ChatStreamEndpointTests(DotNextAIApiFactory factory) : IClassFixture<DotNextAIApiFactory>
{
    private static readonly Uri Stream = new("/api/chat/stream", UriKind.Relative);

    private sealed record ServerSentEvent(string Event, JsonElement Data);

    /// <summary>Parses a text/event-stream body into its events (event + JSON data).</summary>
    private static List<ServerSentEvent> Parse(string body) =>
        body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(block =>
            {
                var lines = block.Split('\n');
                var name = lines.FirstOrDefault(l => l.StartsWith("event:", StringComparison.Ordinal))?[6..].Trim() ?? "message";
                var data = string.Join("\n", lines.Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l[5..].TrimStart()));
                return new ServerSentEvent(name, JsonDocument.Parse(data).RootElement.Clone());
            })
            .ToList();

    [Fact]
    public async Task Post_StreamsTheDeltasThenDone()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("Hello Ana!", "gpt-5.6-luna", Truncated: false));
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var events = Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["delta", "delta", "done"], events.Select(e => e.Event));
        Assert.Equal("Hello Ana!", string.Concat(events.Where(e => e.Event == "delta").Select(e => e.Data.GetProperty("text").GetString())));
        var done = events[^1].Data;
        Assert.Equal("Hello Ana!", done.GetProperty("answer").GetString());
        Assert.Equal("gpt-5.6-luna", done.GetProperty("model").GetString());
        Assert.False(done.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task Post_PassesQuestionHistoryAndImagesLikeTheNonStreamingEndpoint()
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(
            Stream,
            new
            {
                question = "  What is my name?  ",
                history = new[] { new { role = "user", text = "My name is Ana." }, new { role = "assistant", text = "Hi Ana!" } },
                images = new[] { new { mediaType = "image/png", data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==" } },
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = Assert.Single(chat.Calls);
        Assert.Equal("What is my name?", call.Question);
        Assert.Equal(2, call.History.Length);
        Assert.Single(call.Images);
    }

    [Fact]
    public async Task Post_InvalidInput_Returns400ValidationProblemNotAStream()
    {
        var chat = new FakeChatService();
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(Stream, new { question = " " }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task Post_NotConfigured_Returns503()
    {
        using var client = factory.CreateClientWith(new FakeChatService { IsConfigured = false });

        using var response = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(ChatFailure.ModelUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(ChatFailure.Unauthorized, HttpStatusCode.BadGateway)]
    [InlineData(ChatFailure.Timeout, HttpStatusCode.GatewayTimeout)]
    public async Task Post_FailureBeforeTheFirstPiece_ReturnsTheProblemWithItsStatus(ChatFailure failure, HttpStatusCode status)
    {
        using var client = factory.CreateClientWith(FakeChatService.Failing(failure, "Something failed."));

        using var response = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(failure.ToString(), problem.GetProperty("failure").GetString());
    }

    [Fact]
    public async Task Post_FailureMidAnswer_EndsTheStreamWithAnErrorEvent()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", Truncated: false))
        {
            FailMidStream = new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached."),
        };
        using var client = factory.CreateClientWith(chat);

        using var response = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["delta", "error"], events.Select(e => e.Event));
        var error = events[^1].Data;
        Assert.Equal(502, error.GetProperty("status").GetInt32());
        Assert.Equal("Unavailable", error.GetProperty("failure").GetString());
        Assert.Equal("The LiteLLM proxy could not be reached.", error.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_SharesTheRateLimitOfTheChat()
    {
        using var client = factory.CreateClientWith(new FakeChatService(), chatPermitsPerMinute: 1);

        using var first = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);
        using var second = await client.PostAsJsonAsync(Stream, new { question = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}
