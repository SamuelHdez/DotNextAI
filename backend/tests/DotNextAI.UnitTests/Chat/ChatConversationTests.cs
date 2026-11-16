using DotNextAI.Application.Chat;
using DotNextAI.UnitTests.Cli;

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
    public async Task Reset_ForgetsEveryTurn()
    {
        var conversation = new ChatConversation(new FakeChatService());
        await conversation.AskAsync("one", TestContext.Current.CancellationToken);

        conversation.Reset();

        Assert.Empty(conversation.Turns);
        Assert.False(conversation.IsFull);
    }
}
