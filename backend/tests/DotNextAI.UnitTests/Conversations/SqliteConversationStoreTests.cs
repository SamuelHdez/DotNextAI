using DotNextAI.Application.Chat;
using DotNextAI.Application.Conversations;
using DotNextAI.UnitTests.TestSupport;

namespace DotNextAI.UnitTests.Conversations;

public sealed class SqliteConversationStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static SavedExchange Exchange(string question, string answer, TokenUsage? usage = null, params ChatImage[] images) =>
        new(question, images, answer, "gpt-5.6-luna", usage, Truncated: false, Stopped: false);

    [Fact]
    public async Task Folders_AreCreatedListedByNameAndRenamed()
    {
        var store = _database.NewStore();
        var work = await store.CreateFolderAsync("Work", TestContext.Current.CancellationToken);
        await store.CreateFolderAsync("Archive", TestContext.Current.CancellationToken);

        var renamed = await store.RenameFolderAsync(work.Id, "Projects", TestContext.Current.CancellationToken);

        Assert.Equal("Projects", renamed.Name);
        Assert.Equal(["Archive", "Projects"], (await _database.NewStore().ListFoldersAsync(TestContext.Current.CancellationToken)).Select(f => f.Name));
    }

    [Theory]
    [InlineData("work")]
    [InlineData("WORK")]
    public async Task Folders_NamesAreUniqueIgnoringCase(string other)
    {
        var store = _database.NewStore();
        await store.CreateFolderAsync("Work", TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConversationStoreException>(() => store.CreateFolderAsync(other, TestContext.Current.CancellationToken));

        Assert.Equal(StoreFailure.DuplicateFolderName, ex.Failure);
    }

    [Fact]
    public async Task Folders_RenamingToItsOwnNameIsAllowed()
    {
        var store = _database.NewStore();
        var folder = await store.CreateFolderAsync("Work", TestContext.Current.CancellationToken);

        var renamed = await store.RenameFolderAsync(folder.Id, "WORK", TestContext.Current.CancellationToken);

        Assert.Equal("WORK", renamed.Name);
    }

    [Fact]
    public async Task DeletingAFolder_KeepsItsConversationsWithoutAFolder()
    {
        var store = _database.NewStore();
        var folder = await store.CreateFolderAsync("Work", TestContext.Current.CancellationToken);
        var conversation = await store.CreateConversationAsync("Plan", folder.Id, TestContext.Current.CancellationToken);

        await store.DeleteFolderAsync(folder.Id, TestContext.Current.CancellationToken);

        var kept = Assert.Single(await _database.NewStore().ListConversationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(conversation.Id, kept.Id);
        Assert.Null(kept.FolderId);
        Assert.Empty(await _database.NewStore().ListFoldersAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Conversations_ListTheMostRecentlyUpdatedFirst()
    {
        var store = _database.NewStore();
        var older = await store.CreateConversationAsync("Older", null, TestContext.Current.CancellationToken);
        _database.Time.Advance(TimeSpan.FromMinutes(1));
        await store.CreateConversationAsync("Newer", null, TestContext.Current.CancellationToken);
        _database.Time.Advance(TimeSpan.FromMinutes(1));

        await _database.NewStore().AppendExchangeAsync(older.Id, Exchange("Hi", "Hello"), null, TestContext.Current.CancellationToken);

        Assert.Equal(["Older", "Newer"], (await _database.NewStore().ListConversationsAsync(TestContext.Current.CancellationToken)).Select(c => c.Title));
    }

    [Fact]
    public async Task AppendExchange_SavesBothMessagesInOrderWithTotalsAndTitle()
    {
        var store = _database.NewStore();
        var conversation = await store.CreateConversationAsync(ConversationRules.UntitledTitle, null, TestContext.Current.CancellationToken);

        await store.AppendExchangeAsync(conversation.Id, Exchange("My name is Ana.", "Hi Ana!", new TokenUsage(10, 3, 13)), "My name is Ana.", TestContext.Current.CancellationToken);
        var summary = await _database.NewStore().AppendExchangeAsync(conversation.Id, Exchange("What is my name?", "Ana.", new TokenUsage(20, 1, 21)), null, TestContext.Current.CancellationToken);

        Assert.Equal("My name is Ana.", summary.Title);
        Assert.Equal(2, summary.QuestionCount);
        Assert.Equal(new TokenUsage(30, 4, 34), summary.TotalUsage);
        Assert.Equal("gpt-5.6-luna", summary.Model);

        var detail = await _database.NewStore().GetConversationAsync(conversation.Id, TestContext.Current.CancellationToken);
        Assert.Equal(
            [(ChatTurnRole.User, "My name is Ana."), (ChatTurnRole.Assistant, "Hi Ana!"), (ChatTurnRole.User, "What is my name?"), (ChatTurnRole.Assistant, "Ana.")],
            detail!.Messages.Select(m => (m.Role, m.Text)));
        Assert.Equal(new TokenUsage(20, 1, 21), detail.Messages[3].Usage);
        Assert.Null(detail.Messages[2].Usage);
    }

    [Fact]
    public async Task Images_AreStoredWithTheQuestionAndServedOneByOne()
    {
        var store = _database.NewStore();
        var conversation = await store.CreateConversationAsync("Pics", null, TestContext.Current.CancellationToken);

        await store.AppendExchangeAsync(
            conversation.Id,
            Exchange("What are these?", "Two images.", null, new ChatImage(ImageFormats.Png, TestImages.Png), new ChatImage(ImageFormats.Jpeg, TestImages.Jpeg)),
            null,
            TestContext.Current.CancellationToken);

        var question = (await _database.NewStore().GetConversationAsync(conversation.Id, TestContext.Current.CancellationToken))!.Messages[0];
        Assert.Equal([ImageFormats.Png, ImageFormats.Jpeg], question.Images.Select(i => i.MediaType));
        Assert.Equal(TestImages.Png.Length, question.Images[0].Size);

        var image = await _database.NewStore().GetImageAsync(conversation.Id, question.Images[1].Id, TestContext.Current.CancellationToken);
        Assert.Equal(TestImages.Jpeg, image!.Data.ToArray());
        Assert.Null(await _database.NewStore().GetImageAsync(Guid.NewGuid(), question.Images[1].Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingAConversation_DeletesItsMessagesAndImages()
    {
        var store = _database.NewStore();
        var conversation = await store.CreateConversationAsync("Pics", null, TestContext.Current.CancellationToken);
        await store.AppendExchangeAsync(conversation.Id, Exchange("Q", "A", null, new ChatImage(ImageFormats.Png, TestImages.Png)), null, TestContext.Current.CancellationToken);

        await _database.NewStore().DeleteConversationAsync(conversation.Id, TestContext.Current.CancellationToken);

        using var context = _database.NewContext();
        Assert.Empty(context.Messages);
        Assert.Empty(context.MessageImages);
        await Assert.ThrowsAsync<ConversationStoreException>(() => _database.NewStore().DeleteConversationAsync(conversation.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MovingToAMissingFolder_IsRefused()
    {
        var store = _database.NewStore();
        var conversation = await store.CreateConversationAsync("Plan", null, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConversationStoreException>(() => store.MoveConversationAsync(conversation.Id, Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Equal(StoreFailure.FolderNotFound, ex.Failure);
    }

    [Fact]
    public async Task MissingConversation_IsNotFound()
    {
        var store = _database.NewStore();

        Assert.Null(await store.GetConversationAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        var ex = await Assert.ThrowsAsync<ConversationStoreException>(() => store.RenameConversationAsync(Guid.NewGuid(), "x", TestContext.Current.CancellationToken));
        Assert.Equal(StoreFailure.NotFound, ex.Failure);
    }
}
