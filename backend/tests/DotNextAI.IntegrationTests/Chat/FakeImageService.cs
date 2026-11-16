using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;

namespace DotNextAI.IntegrationTests.Chat;

/// <summary>Image service double: records the requests and draws a 1×1 PNG (or fails).</summary>
internal sealed class FakeImageService : IImageService
{
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public bool IsConfigured { get; init; } = true;

    public string? Model => IsConfigured ? "gpt-image-2" : null;

    public ChatServiceException? Failure { get; init; }

    public GeneratedImage? Result { get; init; }

    public List<ImageRequest> Requests { get; } = [];

    public Task<GeneratedImage> GenerateAsync(ImageRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Failure is null
            ? Task.FromResult(Result ?? new GeneratedImage(new ChatImage(ImageFormats.Png, Png), "gpt-image-2", "A tiny black pixel."))
            : Task.FromException<GeneratedImage>(Failure);
    }
}
