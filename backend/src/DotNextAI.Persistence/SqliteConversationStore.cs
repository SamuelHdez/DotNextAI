using DotNextAI.Application.Chat;
using DotNextAI.Application.Conversations;
using DotNextAI.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotNextAI.Persistence;

/// <summary><see cref="IConversationStore"/> over the SQLite database (docs/specs/feature-saved-chats.md).</summary>
internal sealed class SqliteConversationStore(ChatsDbContext db, TimeProvider timeProvider) : IConversationStore
{
    public async Task<IReadOnlyList<FolderInfo>> ListFoldersAsync(CancellationToken cancellationToken) =>
        (await db.Folders.AsNoTracking()
            .OrderBy(folder => folder.Name)
            .Select(folder => new { folder.Id, folder.Name, folder.Icon, folder.Color, Count = folder.Conversations.Count, folder.CreatedAt })
            .ToListAsync(cancellationToken))
        .Select(folder => new FolderInfo(folder.Id, folder.Name, folder.Icon, folder.Color, folder.Count, Utc(folder.CreatedAt)))
        .ToArray();

    public async Task<FolderInfo> CreateFolderAsync(string name, string icon, string color, CancellationToken cancellationToken)
    {
        await EnsureFolderNameIsFreeAsync(name, except: null, cancellationToken);
        var folder = new FolderEntity { Id = Guid.NewGuid(), Name = name, Icon = icon, Color = color, CreatedAt = Now() };
        db.Folders.Add(folder);
        await SaveAsync(cancellationToken);
        return Info(folder, 0);
    }

    public async Task<FolderInfo> UpdateFolderAsync(Guid id, FolderUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == id, cancellationToken) ?? throw FolderNotFound();
        if (update.Name is { } name)
        {
            await EnsureFolderNameIsFreeAsync(name, except: id, cancellationToken);
            folder.Name = name;
        }

        folder.Icon = update.Icon ?? folder.Icon;
        folder.Color = update.Color ?? folder.Color;
        await SaveAsync(cancellationToken);
        var count = await db.Conversations.CountAsync(c => c.FolderId == id, cancellationToken);
        return Info(folder, count);
    }

    private static FolderInfo Info(FolderEntity folder, int count) =>
        new(folder.Id, folder.Name, folder.Icon, folder.Color, count, Utc(folder.CreatedAt));

    public async Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken)
    {
        var folder = await db.Folders.Include(f => f.Conversations).FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw FolderNotFound();
        // The conversations stay, without a folder (also enforced by ON DELETE SET NULL).
        foreach (var conversation in folder.Conversations)
        {
            conversation.FolderId = null;
        }

        db.Folders.Remove(folder);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(CancellationToken cancellationToken) =>
        (await db.Conversations.AsNoTracking()
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken))
        .Select(Summary)
        .ToArray();

    public async Task<ConversationSummary> CreateConversationAsync(string title, Guid? folderId, CancellationToken cancellationToken)
    {
        await EnsureFolderExistsAsync(folderId, cancellationToken);
        var now = Now();
        var conversation = new ConversationEntity { Id = Guid.NewGuid(), Title = title, FolderId = folderId, Cost = 0m, CreatedAt = now, UpdatedAt = now };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return Summary(conversation);
    }

    public async Task<ConversationDetail?> GetConversationAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        // Image metadata only: the bytes are served one by one (GetImageAsync).
        var messages = await db.Messages.AsNoTracking()
            .Where(message => message.ConversationId == id)
            .OrderBy(message => message.Sequence)
            .Select(message => new
            {
                Message = message,
                Images = message.Images.OrderBy(image => image.Position).Select(image => new StoredImageInfo(image.Id, image.MediaType, image.Size)).ToList(),
            })
            .ToListAsync(cancellationToken);

        return new ConversationDetail(
            Summary(conversation),
            messages.Select(row => new StoredMessage(
                row.Message.Id,
                (ChatTurnRole)row.Message.Role,
                row.Message.Text,
                row.Images,
                row.Message.Model,
                TokenUsage.From(row.Message.InputTokens, row.Message.OutputTokens, row.Message.TotalTokens) is { } usage
                    ? usage with { Cost = row.Message.Cost }
                    : null,
                row.Message.Truncated,
                row.Message.Stopped,
                Utc(row.Message.CreatedAt))).ToArray());
    }

    public async Task<ConversationSummary> RenameConversationAsync(Guid id, string title, CancellationToken cancellationToken)
    {
        var conversation = await FindAsync(id, cancellationToken);
        conversation.Title = title;
        await db.SaveChangesAsync(cancellationToken);
        return Summary(conversation);
    }

    public async Task<ConversationSummary> MoveConversationAsync(Guid id, Guid? folderId, CancellationToken cancellationToken)
    {
        var conversation = await FindAsync(id, cancellationToken);
        await EnsureFolderExistsAsync(folderId, cancellationToken);
        conversation.FolderId = folderId;
        await db.SaveChangesAsync(cancellationToken);
        return Summary(conversation);
    }

    public async Task DeleteConversationAsync(Guid id, CancellationToken cancellationToken)
    {
        // Messages and images go with it (ON DELETE CASCADE).
        var deleted = await db.Conversations.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            throw NotFound();
        }
    }

    public async Task<ConversationSummary> AppendExchangeAsync(Guid id, SavedExchange exchange, string? title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        var conversation = await FindAsync(id, cancellationToken);
        var last = await db.Messages.Where(m => m.ConversationId == id).MaxAsync(m => (int?)m.Sequence, cancellationToken) ?? -1;
        var now = Now();

        db.Messages.Add(new MessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = id,
            Sequence = last + 1,
            Role = (int)ChatTurnRole.User,
            Text = exchange.Question,
            CreatedAt = now,
            Images = exchange.Images.Select((image, position) => new MessageImageEntity
            {
                Id = Guid.NewGuid(),
                Position = position,
                MediaType = image.MediaType,
                Size = image.Data.Length,
                Data = image.Data.ToArray(),
            }).ToList(),
        });
        db.Messages.Add(new MessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = id,
            Sequence = last + 2,
            Role = (int)ChatTurnRole.Assistant,
            Text = exchange.Answer,
            Model = exchange.Model,
            InputTokens = exchange.Usage?.InputTokens,
            OutputTokens = exchange.Usage?.OutputTokens,
            TotalTokens = exchange.Usage?.TotalTokens,
            Cost = exchange.Usage?.Cost,
            Truncated = exchange.Truncated,
            Stopped = exchange.Stopped,
            CreatedAt = now,
        });

        conversation.QuestionCount++;
        conversation.Model = exchange.Model ?? conversation.Model;
        if (exchange.Usage is { } usage)
        {
            // Same rule as TokenUsage.Add: one answer without a cost makes the total unknown.
            var total = new TokenUsage(conversation.InputTokens, conversation.OutputTokens, conversation.TotalTokens, conversation.Cost).Add(usage);
            (conversation.InputTokens, conversation.OutputTokens, conversation.TotalTokens, conversation.Cost) =
                (total.InputTokens, total.OutputTokens, total.TotalTokens, total.Cost);
        }

        conversation.UpdatedAt = now;
        if (title is not null)
        {
            conversation.Title = title;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Summary(conversation);
    }

    public async Task<ChatImage?> GetImageAsync(Guid conversationId, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await db.MessageImages.AsNoTracking()
            .Where(i => i.Id == imageId && i.Message!.ConversationId == conversationId)
            .Select(i => new { i.MediaType, i.Data })
            .FirstOrDefaultAsync(cancellationToken);
        return image is null ? null : new ChatImage(image.MediaType, image.Data);
    }

    private static ConversationSummary Summary(ConversationEntity conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.FolderId,
            conversation.Model,
            conversation.QuestionCount,
            new TokenUsage(conversation.InputTokens, conversation.OutputTokens, conversation.TotalTokens, conversation.Cost),
            Utc(conversation.CreatedAt),
            Utc(conversation.UpdatedAt));

    private async Task<ConversationEntity> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw NotFound();

    private async Task EnsureFolderExistsAsync(Guid? folderId, CancellationToken cancellationToken)
    {
        if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id, cancellationToken))
        {
            throw FolderNotFound();
        }
    }

    // The NOCASE collation of the column makes this comparison case-insensitive in SQLite.
    private async Task EnsureFolderNameIsFreeAsync(string name, Guid? except, CancellationToken cancellationToken)
    {
        if (await db.Folders.AnyAsync(f => f.Name == name && f.Id != except, cancellationToken))
        {
            throw DuplicateName(name);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Two requests created the same name at once: the unique index decides.
            throw new ConversationStoreException(StoreFailure.DuplicateFolderName, "A folder with that name already exists.");
        }
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static ConversationStoreException NotFound() => new(StoreFailure.NotFound, "The conversation does not exist.");

    private static ConversationStoreException FolderNotFound() => new(StoreFailure.FolderNotFound, "The folder does not exist.");

    private static ConversationStoreException DuplicateName(string name) =>
        new(StoreFailure.DuplicateFolderName, $"A folder named '{name}' already exists.");
}
