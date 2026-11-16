using DotNextAI.Api.Contracts;
using DotNextAI.Api.RateLimiting;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNextAI.Api.Endpoints;

/// <summary>Backlog step 09: image generation over HTTP (docs/design/api.md#post-apiimages).</summary>
internal static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/images", GenerateAsync)
            .WithName("GenerateImage")
            .WithTags("Images")
            .WithSummary("Generate an image from a description with the proxy's image model (gpt-image).")
            .Accepts<ImageRequestDto>("application/json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireRateLimiting(RateLimitingOptions.ImagePolicy);

        return app;
    }

    private static async Task<Results<Ok<ImageResponseDto>, ValidationProblem, ProblemHttpResult>> GenerateAsync(
        ImageRequestDto request,
        IImageService images,
        CancellationToken cancellationToken)
    {
        if (!ImageRules.TryNormalizePrompt(request.Prompt, out var prompt, out var error))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["prompt"] = [error] });
        }

        if (!images.IsConfigured)
        {
            return ChatProblems.FromException(new ChatServiceException(ChatFailure.NotConfigured, "Image generation is not configured on the server."));
        }

        try
        {
            var image = await images.GenerateAsync(new ImageRequest(prompt, request.Size ?? ImageSize.Square), cancellationToken);
            return TypedResults.Ok(new ImageResponseDto(
                image.Image.MediaType,
                Convert.ToBase64String(image.Image.Data.Span),
                image.Model,
                image.RevisedPrompt,
                TokenUsageDto.From(image.Usage)));
        }
        catch (ChatServiceException ex)
        {
            return ChatProblems.FromException(ex);
        }
    }
}
