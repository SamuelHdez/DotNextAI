using DotNextAI.Application.Chat;
using DotNextAI.UnitTests.Cli;
using DotNextAI.UnitTests.TestSupport;

namespace DotNextAI.UnitTests.Chat;

public class ChatConversationTests
{
    [Fact]
    public async Task AskAsync_RecordsTheQuestionAndTheAnswer()
    {
        var conversation = new ChatConversation(new FakeChatService());

        await conversation.AskAsync("  Hi  ", TestContext.Current.CancellationToken);

        Assert.Equal(
            [new ChatTurn(ChatTurnRole.User, "Hi"), new ChatTurn(ChatTurnRole.Assistant, "answer to Hi")],
            conversation.Turns);
        Assert.Equal(1, conversation.QuestionCount);
    }

    [Fact]
    public async Task AskAsync_SendsASnapshotOfThePreviousTurns()
    {
        var chat = new FakeChatService();
        var conversation = new ChatConversation(chat);

        await conversation.AskAsync("one", TestContext.Current.CancellationToken);
        await conversation.AskAsync("two", TestContext.Current.CancellationToken);

        Assert.Empty(chat.Calls[0].History);
        Assert.Equal(2, chat.Calls[1].History.Length);
        Assert.Equal(4, conversation.Turns.Count);
    }

    [Fact]
    public async Task AskAsync_Failure_LeavesTheConversationUnchanged()
    {
        var conversation = new ChatConversation(FakeChatService.Failing(ChatFailure.Timeout, "Too slow."));

        await Assert.ThrowsAsync<ChatServiceException>(() => conversation.AskAsync("one", TestContext.Current.CancellationToken));

        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public async Task AskAsync_WhenFull_Throws()
    {
        var conversation = new ChatConversation(new FakeChatService());
        for (var i = 0; i < ChatRules.MaxConversationQuestions; i++)
        {
            await conversation.AskAsync($"q{i}", TestContext.Current.CancellationToken);
        }

        Assert.True(conversation.IsFull);
        await Assert.ThrowsAsync<InvalidOperationException>(() => conversation.AskAsync("one more", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AskAsync_WithImages_SendsThemOnceAndKeepsTheTextInTheHistory()
    {
        var chat = new FakeChatService();
        var conversation = new ChatConversation(chat);
        ChatImage[] images = [new(ImageFormats.Png, TestImages.Png)];

        await conversation.AskAsync("What is this?", images, TestContext.Current.CancellationToken);
        await conversation.AskAsync("And its colour?", TestContext.Current.CancellationToken);

        Assert.Single(chat.Calls[0].Images);
        Assert.Empty(chat.Calls[1].Images);
        Assert.Equal(new ChatTurn(ChatTurnRole.User, "What is this?"), chat.Calls[1].History[0]);
    }

    [Fact]
    public async Task AskStreamingAsync_RecordsTheTurnsWhenTheAnswerIsComplete()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("Hello Ana!", "gpt-5.6-luna", Truncated: false));
        var conversation = new ChatConversation(chat);
        var pieces = new List<string>();

        await foreach (var update in conversation.AskStreamingAsync("Hi", [], TestContext.Current.CancellationToken))
        {
            pieces.Add(update.Text);
        }

        Assert.Equal("Hello Ana!", string.Concat(pieces));
        Assert.Equal([new ChatTurn(ChatTurnRole.User, "Hi"), new ChatTurn(ChatTurnRole.Assistant, "Hello Ana!")], conversation.Turns);
    }

    [Fact]
    public async Task AskStreamingAsync_StoppedEarly_RecordsNothing()
    {
        var conversation = new ChatConversation(new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", Truncated: false)));

        await foreach (var _ in conversation.AskStreamingAsync("Hi", [], TestContext.Current.CancellationToken))
        {
            break;
        }

        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public async Task AskStreamingAsync_FailureMidAnswer_RecordsNothing()
    {
        var chat = new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", Truncated: false))
        {
            FailMidStream = new ChatServiceException(ChatFailure.Unavailable, "Dropped."),
        };
        var conversation = new ChatConversation(chat);

        await Assert.ThrowsAsync<ChatServiceException>(async () =>
        {
            await foreach (var _ in conversation.AskStreamingAsync("Hi", [], TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Empty(conversation.Turns);
    }

    [Fact]
    public async Task AskAsync_SendsTheChosenModelAndKeepsTheHistoryAcrossModels()
    {
        var chat = new FakeChatService();
        var conversation = new ChatConversation(chat);

        await conversation.AskAsync("one", TestContext.Current.CancellationToken);
        conversation.Model = "gpt-5.6-terra";
        await conversation.AskAsync("two", TestContext.Current.CancellationToken);

        Assert.Null(chat.Calls[0].Model);
        Assert.Equal("gpt-5.6-terra", chat.Calls[1].Model);
        Assert.Equal(2, chat.Calls[1].History.Length);
    }

    [Fact]
    public async Task Reset_ForgetsEveryTurn()
    {
        var conversation = new ChatConversation(new FakeChatService());
        await conversation.AskAsync("one", TestContext.Current.CancellationToken);

        conversation.Reset();

        Assert.Empty(conversation.Turns);
        Assert.False(conversation.IsFull);
    }
}
