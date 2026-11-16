using DotNextAI.Application.Chat;
using DotNextAI.Application.Conversations;
using DotNextAI.UnitTests.Cli;
using DotNextAI.UnitTests.TestSupport;

namespace DotNextAI.UnitTests.Conversations;

public sealed class ConversationServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Guid> NewConversationAsync() =>
        (await _database.NewStore().CreateConversationAsync(ConversationRules.UntitledTitle, null, TestContext.Current.CancellationToken)).Id;

    private static async Task<List<ConversationStreamUpdate>> DrainAsync(IAsyncEnumerable<ConversationStreamUpdate> updates)
    {
        var all = new List<ConversationStreamUpdate>();
        await foreach (var update in updates)
        {
            all.Add(update);
        }

        return all;
    }

    [Fact]
    public async Task Ask_SavesTheExchangeAndNamesTheConversationAfterTheFirstQuestion()
    {
        var id = await NewConversationAsync();
        var chat = new FakeChatService(_ => new ChatAnswer("Hello Ana!", "gpt-5.6-luna", false, new TokenUsage(10, 2, 12)));
        var service = new ConversationService(_database.NewStore(), chat);

        var updates = await DrainAsync(service.AskStreamingAsync(id, "  My name is Ana.  ", [], null, TestContext.Current.CancellationToken));

        var saved = updates[^1].Saved!;
        Assert.Equal("My name is Ana.", saved.Title);
        Assert.Equal(1, saved.QuestionCount);
        Assert.Equal(new TokenUsage(10, 2, 12), saved.TotalUsage);
        Assert.Equal("Hello Ana!", string.Concat(updates.Select(u => u.Update.Text)));
        var detail = await _database.NewStore().GetConversationAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(["My name is Ana.", "Hello Ana!"], detail!.Messages.Select(m => m.Text));
    }

    [Fact]
    public async Task Ask_SendsTheSavedHistoryAndKeepsTheTitle()
    {
        var id = await NewConversationAsync();
        var chat = new FakeChatService();
        await DrainAsync(new ConversationService(_database.NewStore(), chat).AskStreamingAsync(id, "one", [], null, TestContext.Current.CancellationToken));

        var updates = await DrainAsync(new ConversationService(_database.NewStore(), chat).AskStreamingAsync(id, "two", [], "gpt-5.6-terra", TestContext.Current.CancellationToken));

        Assert.Equal([new ChatTurn(ChatTurnRole.User, "one"), new ChatTurn(ChatTurnRole.Assistant, "answer to one")], chat.Calls[1].History);
        Assert.Equal("gpt-5.6-terra", chat.Calls[1].Model);
        Assert.Equal("one", updates[^1].Saved!.Title);
    }

    [Fact]
    public async Task Ask_SavesTheImagesWithTheQuestion()
    {
        var id = await NewConversationAsync();
        var service = new ConversationService(_database.NewStore(), new FakeChatService());

        await DrainAsync(service.AskStreamingAsync(id, "What is this?", [new ChatImage(ImageFormats.Png, TestImages.Png)], null, TestContext.Current.CancellationToken));

        var question = (await _database.NewStore().GetConversationAsync(id, TestContext.Current.CancellationToken))!.Messages[0];
        Assert.Equal(ImageFormats.Png, Assert.Single(question.Images).MediaType);
    }

    [Fact]
    public async Task StoppingEarly_SavesTheTextSoFarMarkedAsStopped()
    {
        var id = await NewConversationAsync();
        var service = new ConversationService(_database.NewStore(), new FakeChatService(_ => new ChatAnswer("one two three", "gpt-5.6-luna", false)));

        await foreach (var _ in service.AskStreamingAsync(id, "Count", [], "gpt-5.6-terra", TestContext.Current.CancellationToken))
        {
            break;
        }

        var answer = (await _database.NewStore().GetConversationAsync(id, TestContext.Current.CancellationToken))!.Messages[1];
        Assert.Equal("one ", answer.Text);
        Assert.True(answer.Stopped);
        Assert.Equal("gpt-5.6-terra", answer.Model);
        Assert.Null(answer.Usage);
    }

    [Fact]
    public async Task AFailedAnswer_SavesNothing()
    {
        var id = await NewConversationAsync();
        var chat = new FakeChatService(_ => new ChatAnswer("one two", "gpt-5.6-luna", false))
        {
            FailMidStream = new ChatServiceException(ChatFailure.Unavailable, "Dropped."),
        };
        var service = new ConversationService(_database.NewStore(), chat);

        await Assert.ThrowsAsync<ChatServiceException>(() => DrainAsync(service.AskStreamingAsync(id, "Hi", [], null, TestContext.Current.CancellationToken)));

        var detail = await _database.NewStore().GetConversationAsync(id, TestContext.Current.CancellationToken);
        Assert.Empty(detail!.Messages);
        Assert.Equal(ConversationRules.UntitledTitle, detail.Summary.Title);
    }

    [Fact]
    public async Task AFullConversation_IsRefused()
    {
        var id = await NewConversationAsync();
        var store = _database.NewStore();
        for (var i = 0; i < ChatRules.MaxConversationQuestions; i++)
        {
            await store.AppendExchangeAsync(id, new SavedExchange($"q{i}", [], "a", null, null, false, false), null, TestContext.Current.CancellationToken);
        }

        var chat = new FakeChatService();
        var ex = await Assert.ThrowsAsync<ConversationStoreException>(
            () => DrainAsync(new ConversationService(_database.NewStore(), chat).AskStreamingAsync(id, "one more", [], null, TestContext.Current.CancellationToken)));

        Assert.Equal(StoreFailure.ConversationFull, ex.Failure);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task AMissingConversation_IsNotFound()
    {
        var service = new ConversationService(_database.NewStore(), new FakeChatService());

        var ex = await Assert.ThrowsAsync<ConversationStoreException>(
            () => DrainAsync(service.AskStreamingAsync(Guid.NewGuid(), "Hi", [], null, TestContext.Current.CancellationToken)));

        Assert.Equal(StoreFailure.NotFound, ex.Failure);
    }

    [Theory]
    [InlineData("Plan the team offsite", "Plan the team offsite")]
    [InlineData("  first line\nsecond line", "first line")]
    [InlineData("many    spaces   here", "many spaces here")]
    [InlineData("A question that is clearly much longer than sixty characters to be a title", "A question that is clearly much longer than sixty characters…")]
    public void TitleFrom_TakesTheFirstLineCutAtAWord(string question, string title)
    {
        Assert.Equal(title, ConversationRules.TitleFrom(question));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Work", true)]
    [InlineData("tab\there", false)]
    public void TryNormalizeFolderName_RejectsEmptyAndControlCharacters(string name, bool valid)
    {
        Assert.Equal(valid, ConversationRules.TryNormalizeFolderName(name, out _, out _));
        Assert.False(ConversationRules.TryNormalizeFolderName(new string('a', ConversationRules.MaxFolderNameLength + 1), out _, out _));
    }
}
