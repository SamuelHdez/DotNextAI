using DotNextAI.Application.Chat;

namespace DotNextAI.Api.Contracts;

/// <summary>Body of <c>POST /api/chat</c> (docs/design/api.md).</summary>
/// <param name="Question">The new question.</param>
/// <param name="History">Previous turns of the conversation, oldest first; omitted or empty for a first question.</param>
/// <param name="Images">Images attached to this question (step 06); omitted or empty when there are none.</param>
/// <param name="Model">Chat model from GET /api/models (step 08); omitted for the default model.</param>
public sealed record ChatRequestDto(
    string? Question,
    IReadOnlyList<ChatTurnDto?>? History,
    IReadOnlyList<ChatImageDto?>? Images = null,
    string? Model = null);

/// <summary>An attached image: its media type and the file bytes in base64 (no <c>data:</c> prefix).</summary>
public sealed record ChatImageDto(string? MediaType, string? Data);

/// <summary>One turn of the history. <c>role</c> is <c>"user"</c> or <c>"assistant"</c>.</summary>
public sealed record ChatTurnDto(ChatTurnRole? Role, string? Text);

/// <summary>Answer of <c>POST /api/chat</c>.</summary>
/// <param name="Answer">Answer text.</param>
/// <param name="Model">Model that answered.</param>
/// <param name="Truncated">True when the answer stopped at the maximum output length.</param>
/// <param name="Usage">Tokens of this call (step 10); null when the proxy does not report them.</param>
public sealed record ChatResponseDto(string Answer, string Model, bool Truncated, TokenUsageDto? Usage = null);

/// <summary>Tokens of one call. Input tokens include the history sent with the question.</summary>
/// <param name="Cost">US dollars at the proxy's prices; null when the model has no price or a call of the total had none.</param>
public sealed record TokenUsageDto(long InputTokens, long OutputTokens, long TotalTokens, decimal? Cost = null)
{
    public static TokenUsageDto? From(DotNextAI.Application.Chat.TokenUsage? usage) =>
        usage is null ? null : new(usage.InputTokens, usage.OutputTokens, usage.TotalTokens, usage.Cost);
}

/// <summary>Answer of <c>GET /api/models</c>: the chat models of the proxy, the default first.</summary>
public sealed record ModelsResponseDto(string DefaultModel, IReadOnlyList<ChatModelDto> Models);

/// <summary>A chat model; the token limits are null when the proxy does not report them.</summary>
public sealed record ChatModelDto(string Id, bool IsDefault, int? MaxInputTokens, int? MaxOutputTokens);

/// <summary>Body of <c>POST /api/images</c> (step 09). <c>size</c> is "square" (default), "landscape" or "portrait".</summary>
public sealed record ImageRequestDto(string? Prompt, DotNextAI.Application.Images.ImageSize? Size = null);

/// <summary>A generated image: the file in base64 (no <c>data:</c> prefix) with its media type.</summary>
public sealed record ImageResponseDto(string MediaType, string Data, string Model, string? RevisedPrompt, TokenUsageDto? Usage = null);

// Saved chats (docs/design/api.md#saved-chats).

public sealed record FolderDto(Guid Id, string Name, string Icon, string Color, int ConversationCount, DateTimeOffset CreatedAt);

/// <summary>
/// Body of <c>POST /api/folders</c> (name required; icon and colour default to "folder" and "default") and of
/// <c>PATCH /api/folders/{id}</c> (each field optional, at least one).
/// </summary>
public sealed record FolderRequestDto(string? Name, string? Icon = null, string? Color = null);

public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    Guid? FolderId,
    string? Model,
    int QuestionCount,
    TokenUsageDto Usage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Body of <c>POST /api/conversations</c>: both optional (default title, no folder).</summary>
public sealed record CreateConversationDto(string? Title, Guid? FolderId);

public sealed record RenameConversationDto(string? Title);

/// <summary>Body of <c>PUT /api/conversations/{id}/folder</c>: the folder, or null for no folder.</summary>
public sealed record MoveConversationDto(Guid? FolderId);

/// <summary>An image of a saved message; <c>url</c> serves its bytes.</summary>
public sealed record StoredImageDto(Guid Id, string MediaType, long Size, string Url);

public sealed record StoredMessageDto(
    Guid Id,
    DotNextAI.Application.Chat.ChatTurnRole Role,
    string Text,
    IReadOnlyList<StoredImageDto> Images,
    string? Model,
    TokenUsageDto? Usage,
    bool Truncated,
    bool Stopped,
    DateTimeOffset CreatedAt);

public sealed record ConversationDetailDto(ConversationSummaryDto Conversation, IReadOnlyList<StoredMessageDto> Messages);

/// <summary>Body of <c>POST /api/conversations/{id}/messages/stream</c>: like a chat request, without history.</summary>
public sealed record ConversationMessageDto(string? Question, IReadOnlyList<ChatImageDto?>? Images = null, string? Model = null);

