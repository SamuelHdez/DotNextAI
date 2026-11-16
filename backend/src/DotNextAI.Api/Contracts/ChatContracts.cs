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
public sealed record ChatResponseDto(string Answer, string Model, bool Truncated);

/// <summary>Answer of <c>GET /api/models</c>: the chat models of the proxy, the default first.</summary>
public sealed record ModelsResponseDto(string DefaultModel, IReadOnlyList<ChatModelDto> Models);

/// <summary>A chat model; the token limits are null when the proxy does not report them.</summary>
public sealed record ChatModelDto(string Id, bool IsDefault, int? MaxInputTokens, int? MaxOutputTokens);

/// <summary>Body of <c>POST /api/images</c> (step 09). <c>size</c> is "square" (default), "landscape" or "portrait".</summary>
public sealed record ImageRequestDto(string? Prompt, DotNextAI.Application.Images.ImageSize? Size = null);

/// <summary>A generated image: the file in base64 (no <c>data:</c> prefix) with its media type.</summary>
public sealed record ImageResponseDto(string MediaType, string Data, string Model, string? RevisedPrompt);

