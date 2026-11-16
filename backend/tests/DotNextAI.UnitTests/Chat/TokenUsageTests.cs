using DotNextAI.Application.Chat;
using DotNextAI.UnitTests.Cli;

namespace DotNextAI.UnitTests.Chat;

public class TokenUsageTests
{
    [Fact]
    public void From_ReportedCounters_KeepsThem()
    {
        Assert.Equal(new TokenUsage(10, 5, 15), TokenUsage.From(10, 5, 15));
    }

    [Fact]
    public void From_MissingTotal_IsInputPlusOutput()
    {
        Assert.Equal(new TokenUsage(10, 5, 15), TokenUsage.From(10, 5, null));
    }

    [Fact]
    public void From_NothingReported_IsNull()
    {
        Assert.Null(TokenUsage.From(null, null, null));
    }

    [Fact]
    public void Add_SumsEveryCounter()
    {
        Assert.Equal(new TokenUsage(11, 7, 18), new TokenUsage(10, 5, 15).Add(new TokenUsage(1, 2, 3)));
    }

    [Fact]
    public async Task Conversation_TotalUsage_SumsTheAnswersAndResetsWithTheConversation()
    {
        var calls = 0;
        var chat = new FakeChatService(q => new ChatAnswer($"answer {++calls}", "gpt-5.6-luna", false, calls == 2 ? null : new TokenUsage(100 * calls, 10, (100 * calls) + 10)));
        var conversation = new ChatConversation(chat);

        await conversation.AskAsync("one", TestContext.Current.CancellationToken);
        await conversation.AskAsync("two", TestContext.Current.CancellationToken);
        await conversation.AskAsync("three", TestContext.Current.CancellationToken);

        // The second answer reported no usage and adds nothing.
        Assert.Equal(new TokenUsage(400, 20, 420), conversation.TotalUsage);

        conversation.Reset();
        Assert.Equal(TokenUsage.Zero, conversation.TotalUsage);
    }

    [Fact]
    public async Task Conversation_TotalUsage_CountsStreamedAnswersButNotFailures()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two", "gpt-5.6-luna", false, new TokenUsage(8, 2, 10)))
        {
            FailMidStream = null,
        };
        var conversation = new ChatConversation(chat);

        await foreach (var _ in conversation.AskStreamingAsync("Hi", [], TestContext.Current.CancellationToken))
        {
        }

        Assert.Equal(new TokenUsage(8, 2, 10), conversation.TotalUsage);

        var failing = new ChatConversation(FakeChatService.Failing(ChatFailure.Timeout, "Too slow."));
        await Assert.ThrowsAsync<ChatServiceException>(() => failing.AskAsync("Hi", TestContext.Current.CancellationToken));
        Assert.Equal(TokenUsage.Zero, failing.TotalUsage);
    }
}
