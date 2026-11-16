using DotNextAI.Application.Chat;

namespace DotNextAI.Application.Images;

/// <summary>Shape of a generated image (gpt-image sizes).</summary>
public enum ImageSize
{
    /// <summary>1024 × 1024.</summary>
    Square = 0,

    /// <summary>1536 × 1024.</summary>
    Landscape,

    /// <summary>1024 × 1536.</summary>
    Portrait,
}

/// <summary>What to draw (backlog step 09).</summary>
/// <param name="Prompt">Description of the image, 1-<see cref="ImageRules.MaxPromptLength"/> characters.</param>
/// <param name="Size">Shape of the image.</param>
public sealed record ImageRequest(string Prompt, ImageSize Size = ImageSize.Square);

/// <summary>A generated image.</summary>
/// <param name="Image">The file: media type detected from its bytes, and the bytes.</param>
/// <param name="Model">Image model that drew it.</param>
/// <param name="RevisedPrompt">The prompt as the model rewrote it, when it reports one.</param>
public sealed record GeneratedImage(ChatImage Image, string Model, string? RevisedPrompt);

/// <summary>
/// Generates images with the image model of the LiteLLM proxy (<c>Llm:ImageModel</c>, e.g. gpt-image-2). Failures are
/// <see cref="ChatServiceException"/> with the same <see cref="ChatFailure"/> values as the chat.
/// </summary>
public interface IImageService
{
    /// <summary>Whether the proxy and the image model are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>The configured image model, or null when image generation is not configured.</summary>
    string? Model { get; }

    /// <exception cref="ArgumentException">The prompt breaks <see cref="ImageRules"/>.</exception>
    /// <exception cref="ChatServiceException">The image could not be generated.</exception>
    Task<GeneratedImage> GenerateAsync(ImageRequest request, CancellationToken cancellationToken);
}

/// <summary>Input rules for image generation, shared by every front end.</summary>
public static class ImageRules
{
    /// <summary>Longest prompt in characters: a detailed description fits; a pasted document does not.</summary>
    public const int MaxPromptLength = 4_000;

    public static bool TryNormalizePrompt(
        string? prompt,
        out string normalized,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        normalized = prompt?.Trim() ?? string.Empty;
        error = normalized.Length switch
        {
            0 => "The image description is empty.",
            > MaxPromptLength => $"The image description is too long ({normalized.Length:N0} characters; the maximum is {MaxPromptLength:N0}).",
            _ => null,
        };

        if (error is null)
        {
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
