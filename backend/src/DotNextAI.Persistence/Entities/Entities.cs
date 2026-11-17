namespace DotNextAI.Persistence.Entities;

// EF Core entities of the saved chats. Times are UTC DateTime: SQLite cannot order DateTimeOffset values.

internal sealed class FolderEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string Icon { get; set; } = DotNextAI.Application.Conversations.FolderAppearance.DefaultIcon;

    public string Color { get; set; } = DotNextAI.Application.Conversations.FolderAppearance.DefaultColor;

    public DateTime CreatedAt { get; set; }

    public List<ConversationEntity> Conversations { get; set; } = [];
}

internal sealed class ConversationEntity
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public Guid? FolderId { get; set; }

    public FolderEntity? Folder { get; set; }

    /// <summary>Model of the last answer.</summary>
    public string? Model { get; set; }

    public int QuestionCount { get; set; }

    // Running token totals (step 10), kept here so lists need no aggregation over the messages.
    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long TotalTokens { get; set; }

    /// <summary>
    /// Running cost in US dollars (docs/specs/feature-token-metrics.md): 0 for a new chat, null once an answer with
    /// tokens has no cost, and null for chats saved before costs existed.
    /// </summary>
    public decimal? Cost { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<MessageEntity> Messages { get; set; } = [];
}

internal sealed class MessageEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public ConversationEntity? Conversation { get; set; }

    /// <summary>Order within the conversation, from 0.</summary>
    public int Sequence { get; set; }

    /// <summary>0 = user, 1 = assistant (<c>ChatTurnRole</c>).</summary>
    public int Role { get; set; }

    public required string Text { get; set; }

    public string? Model { get; set; }

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    public long? TotalTokens { get; set; }

    /// <summary>Cost of the answer in US dollars at the proxy's prices when it was saved; null when unknown.</summary>
    public decimal? Cost { get; set; }

    public bool Truncated { get; set; }

    public bool Stopped { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<MessageImageEntity> Images { get; set; } = [];
}

/// <summary>An image of a user message. Its bytes live in their own table so lists never load them.</summary>
internal sealed class MessageImageEntity
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public MessageEntity? Message { get; set; }

    public int Position { get; set; }

    public required string MediaType { get; set; }

    public long Size { get; set; }

#pragma warning disable CA1819 // An EF Core BLOB column maps to byte[].
    public required byte[] Data { get; set; }
#pragma warning restore CA1819
}
