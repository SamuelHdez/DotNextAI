namespace DotNextAI.Application.Chat;

/// <summary>An image attached to a question (backlog step 06). Sent to the model with that question only.</summary>
/// <param name="MediaType">One of <see cref="ImageFormats.Supported"/>, matching the bytes.</param>
/// <param name="Data">The image file bytes (at most <see cref="ChatRules.MaxImageBytes"/>).</param>
public sealed record ChatImage(string MediaType, ReadOnlyMemory<byte> Data);
