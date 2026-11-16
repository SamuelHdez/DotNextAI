using System.Diagnostics;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNextAI.Infrastructure.Images;

/// <summary>
/// Backlog step 09: <c>POST {endpoint}images/generations</c> (OpenAI images API through LiteLLM) with
/// <c>Llm:ImageModel</c>. gpt-image models always answer base64 (<c>b64_json</c>) and reject <c>response_format</c>,
/// so the call is made directly instead of through the SDK's image client. One image per call, no retries: a retry
/// would draw (and bill) a second image.
/// </summary>
internal sealed partial class LiteLlmImageService(
    IOptions<LlmOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<LiteLlmImageService> logger) : IImageService
{
    private readonly LlmOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured && !string.IsNullOrWhiteSpace(_options.ImageModel);

    public string? Model => IsConfigured ? _options.ImageModel : null;

    public async Task<GeneratedImage> GenerateAsync(ImageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ImageRules.TryNormalizePrompt(request.Prompt, out var prompt, out var error))
        {
            throw new ArgumentException(error, nameof(request));
        }

        if (!IsConfigured)
        {
            throw new ChatServiceException(ChatFailure.NotConfigured, "Image generation is not configured (Llm:Endpoint, Llm:ApiKey and Llm:ImageModel).");
        }

        var model = _options.ImageModel!;
        var started = Stopwatch.GetTimestamp();
        string body;
        try
        {
            body = await LiteLlmHttp.SendAsync(
                httpClientFactory,
                _options,
                HttpMethod.Post,
                "images/generations",
                new { model, prompt, n = 1, size = SizeOf(request.Size) },
                model,
                cancellationToken);
        }
        catch (ChatServiceException ex)
        {
            LogGenerationFailed(model, ex.Failure);
            throw;
        }

        var image = Parse(body, model);
        var bytes = image.Image.Data.Length;
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogGenerated(model, bytes, elapsedMs);
        return image;
    }

    public static string SizeOf(ImageSize size) => size switch
    {
        ImageSize.Landscape => "1536x1024",
        ImageSize.Portrait => "1024x1536",
        _ => "1024x1024",
    };

    private static GeneratedImage Parse(string body, string model)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                throw NoImage();
            }

            var first = data[0];
            var base64 = first.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String ? b64.GetString() : null;
            if (string.IsNullOrEmpty(base64))
            {
                throw NoImage();
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                throw NoImage();
            }

            var mediaType = ImageFormats.Detect(bytes) ?? throw NoImage();
            var revised = first.TryGetProperty("revised_prompt", out var revisedElement) && revisedElement.ValueKind == JsonValueKind.String
                ? revisedElement.GetString()
                : null;
            return new GeneratedImage(new ChatImage(mediaType, bytes), model, string.IsNullOrWhiteSpace(revised) ? null : revised);
        }
        catch (JsonException ex)
        {
            throw new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy answered the image request with invalid JSON.", ex);
        }
    }

    private static ChatServiceException NoImage() => new(ChatFailure.EmptyAnswer, "The model returned no image.");

    // Only metadata: prompts are never logged.
    [LoggerMessage(Level = LogLevel.Information, Message = "Image generated with {Model}: {Bytes} bytes in {ElapsedMs:F0} ms")]
    private partial void LogGenerated(string model, int bytes, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image generation with {Model} failed ({Failure})")]
    private partial void LogGenerationFailed(string model, ChatFailure failure);
}
