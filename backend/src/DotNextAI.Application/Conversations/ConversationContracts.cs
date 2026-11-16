using DotNextAI.Application.Chat;

namespace DotNextAI.Application.Conversations;

/// <summary>A folder of saved conversations (one level, no nesting).</summary>
public sealed record FolderInfo(Guid Id, string Name, int ConversationCount, DateTimeOffset CreatedAt);

/// <summary>A saved conversation without its messages, for lists.</summary>
/// <param name="Model">Model of the last answer, when there is one.</param>
/// <param name="QuestionCount">Answered questions, for the 50-question limit.</param>
/// <param name="TotalUsage">Tokens of every answer so far (step 10).</param>
public sealed record ConversationSummary(
    Guid Id,
    string Title,
    Guid? FolderId,
    string? Model,
    int QuestionCount,
    TokenUsage TotalUsage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>An image stored with a question: metadata only; the bytes are read with <see cref="IConversationStore.GetImageAsync"/>.</summary>
public sealed record StoredImageInfo(Guid Id, string MediaType, long Size);

/// <summary>A saved message. Assistant messages carry the model, the usage and whether they were cut or stopped.</summary>
public sealed record StoredMessage(
    Guid Id,
    ChatTurnRole Role,
    string Text,
    IReadOnlyList<StoredImageInfo> Images,
    string? Model,
    TokenUsage? Usage,
    bool Truncated,
    bool Stopped,
    DateTimeOffset CreatedAt);

/// <summary>A saved conversation with its messages, oldest first.</summary>
public sealed record ConversationDetail(ConversationSummary Summary, IReadOnlyList<StoredMessage> Messages);

/// <summary>An answered question to save: the question with its images, and the answer (complete or stopped).</summary>
public sealed record SavedExchange(
    string Question,
    IReadOnlyList<ChatImage> Images,
    string Answer,
    string? Model,
    TokenUsage? Usage,
    bool Truncated,
    bool Stopped);

/// <summary>Why a conversation or folder operation was refused.</summary>
public enum StoreFailure
{
    NotFound = 0,
    FolderNotFound,
    DuplicateFolderName,

    /// <summary>The conversation reached <see cref="Chat.ChatRules.MaxConversationQuestions"/>.</summary>
    ConversationFull,
}

/// <summary>A refused operation; the message is safe to show.</summary>
public sealed class ConversationStoreException : Exception
{
    public ConversationStoreException()
    {
    }

    public ConversationStoreException(string message)
        : base(message)
    {
    }

    public ConversationStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ConversationStoreException(StoreFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public StoreFailure Failure { get; }
}

/// <summary>
/// Saved conversations and their folders (SQLite, single local user). Names are trimmed and checked by
/// <see cref="ConversationRules"/> before they get here.
/// </summary>
public interface IConversationStore
{
    Task<IReadOnlyList<FolderInfo>> ListFoldersAsync(CancellationToken cancellationToken);

    /// <exception cref="ConversationStoreException"><see cref="StoreFailure.DuplicateFolderName"/>.</exception>
    Task<FolderInfo> CreateFolderAsync(string name, CancellationToken cancellationToken);

    /// <exception cref="ConversationStoreException">Not found or duplicate name.</exception>
    Task<FolderInfo> RenameFolderAsync(Guid id, string name, CancellationToken cancellationToken);

    /// <summary>Deletes the folder; its conversations stay, without a folder.</summary>
    /// <exception cref="ConversationStoreException">Not found.</exception>
    Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every conversation, the most recently updated first.</summary>
    Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(CancellationToken cancellationToken);

    /// <exception cref="ConversationStoreException">The folder does not exist.</exception>
    Task<ConversationSummary> CreateConversationAsync(string title, Guid? folderId, CancellationToken cancellationToken);

    /// <returns>Null when the conversation does not exist.</returns>
    Task<ConversationDetail?> GetConversationAsync(Guid id, CancellationToken cancellationToken);

    /// <exception cref="ConversationStoreException">Not found.</exception>
    Task<ConversationSummary> RenameConversationAsync(Guid id, string title, CancellationToken cancellationToken);

    /// <param name="folderId">Target folder, or null for no folder.</param>
    /// <exception cref="ConversationStoreException">Conversation or folder not found.</exception>
    Task<ConversationSummary> MoveConversationAsync(Guid id, Guid? folderId, CancellationToken cancellationToken);

    /// <summary>Deletes the conversation with its messages and images.</summary>
    /// <exception cref="ConversationStoreException">Not found.</exception>
    Task DeleteConversationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the question (with its images) and the answer. <paramref name="title"/> replaces the title when given
    /// (first question of an untitled conversation).
    /// </summary>
    /// <exception cref="ConversationStoreException">Not found.</exception>
    Task<ConversationSummary> AppendExchangeAsync(Guid id, SavedExchange exchange, string? title, CancellationToken cancellationToken);

    /// <returns>The image file, or null when the conversation or the image does not exist.</returns>
    Task<ChatImage?> GetImageAsync(Guid conversationId, Guid imageId, CancellationToken cancellationToken);
}
