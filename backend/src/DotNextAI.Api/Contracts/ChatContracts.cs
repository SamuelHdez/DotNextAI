using DotNextAI.Application.Chat;

namespace DotNextAI.Api.Contracts;

/// <summary>Body of <c>POST /api/chat</c> (docs/design/api.md).</summary>
/// <param name="Question">The new question.</param>
/// <param name="History">Previous turns of the conversation, oldest first; omitted or empty for a first question.</param>
public sealed record ChatRequestDto(string? Question, IReadOnlyList<ChatTurnDto?>? History);

/// <summary>One turn of the history. <c>role</c> is <c>"user"</c> or <c>"assistant"</c>.</summary>
public sealed record ChatTurnDto(ChatTurnRole? Role, string? Text);

/// <summary>Answer of <c>POST /api/chat</c>.</summary>
/// <param name="Answer">Answer text.</param>
/// <param name="Model">Model that answered.</param>
/// <param name="Truncated">True when the answer stopped at the maximum output length.</param>
public sealed record ChatResponseDto(string Answer, string Model, bool Truncated);
