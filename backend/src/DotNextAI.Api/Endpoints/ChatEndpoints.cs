using DotNextAI.Api.Contracts;
using DotNextAI.Api.RateLimiting;
using DotNextAI.Application.Chat;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNextAI.Api.Endpoints;

/// <summary>Backlog step 04: the chat service over HTTP (docs/design/api.md).</summary>
internal static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", AskAsync)
            .WithName("Chat")
            .WithTags("Chat")
            .WithSummary("Ask a question after the previous turns of a conversation.")
            .WithDescription(
                "Stateless: the client keeps the conversation and sends its history with every question. " +
                "Errors are ProblemDetails with a `failure` extension naming the reason.")
            .Accepts<ChatRequestDto>("application/json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireRateLimiting(RateLimitingOptions.ChatPolicy);

        app.MapPost("/api/chat/stream", AskStreamingAsync)
            .WithName("ChatStream")
            .WithTags("Chat")
            .WithSummary("Same as POST /api/chat, with the answer streamed as Server-Sent Events.")
            .WithDescription(
                "Events: `delta` {text} for each piece, then `done` {answer, model, truncated}, or `error` " +
                "{status, failure, title, detail} if the answer breaks after it started. Failures before the first piece " +
                "are ProblemDetails with their status code, as in POST /api/chat.")
            .Accepts<ChatRequestDto>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireRateLimiting(RateLimitingOptions.ChatPolicy);

        return app;
    }

    private static async Task<Results<Ok<ChatResponseDto>, ValidationProblem, ProblemHttpResult>> AskAsync(
        ChatRequestDto request,
        IChatService chat,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var input, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!chat.IsConfigured)
        {
            return ChatProblems.NotConfigured();
        }

        try
        {
            var answer = await chat.AskAsync(input.Question, input.History, input.Images, cancellationToken);
            return TypedResults.Ok(new ChatResponseDto(answer.Text, answer.Model, answer.Truncated));
        }
        catch (ChatServiceException ex)
        {
            return ChatProblems.FromException(ex);
        }
    }

    /// <summary>
    /// Step 07. The first update is awaited before the stream starts, so the usual failures (key rejected, model
    /// unavailable, timeout) still answer with their ProblemDetails and status code. Only a failure after the first
    /// piece of text, when the 200 headers are already sent, arrives as an <c>error</c> event.
    /// </summary>
    private static async Task<IResult> AskStreamingAsync(ChatRequestDto request, IChatService chat, CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var input, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!chat.IsConfigured)
        {
            return ChatProblems.NotConfigured();
        }

        var updates = chat.AskStreamingAsync(input.Question, input.History, input.Images, cancellationToken).GetAsyncEnumerator(cancellationToken);
        bool hasFirst;
        try
        {
            hasFirst = await updates.MoveNextAsync();
        }
        catch (ChatServiceException ex)
        {
            await updates.DisposeAsync();
            return ChatProblems.FromException(ex);
        }
        catch
        {
            await updates.DisposeAsync();
            throw;
        }

        return TypedResults.ServerSentEvents(ChatStreamEvents.From(updates, hasFirst, cancellationToken));
    }

    private sealed record ValidInput(string Question, ChatTurn[] History, ChatImage[] Images);

    /// <summary>The rules of both chat endpoints: 400 ValidationProblem with errors per field.</summary>
    private static bool TryValidate(
        ChatRequestDto request,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ValidInput? input,
        out Dictionary<string, string[]> errors)
    {
        input = null;
        errors = [];
        if (!ChatRules.TryNormalizeQuestion(request.Question, out var question, out var questionError))
        {
            errors["question"] = [questionError];
        }

        var turns = request.History ?? [];
        if (turns.Any(turn => turn?.Role is null))
        {
            errors["history"] = ["Every turn needs a role: \"user\" or \"assistant\"."];
        }

        var history = turns
            .Select(turn => turn?.Role is { } role ? new ChatTurn(role, turn.Text ?? string.Empty) : null)
            .ToArray();
        if (!errors.ContainsKey("history") && !ChatRules.TryValidateHistory(history, out var historyError))
        {
            errors["history"] = [historyError];
        }

        var images = DecodeImages(request.Images, out var imagesError);
        if (imagesError is null && !ChatRules.TryValidateImages(images, out var rulesError))
        {
            imagesError = rulesError;
        }

        if (imagesError is not null)
        {
            errors["images"] = [imagesError];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        input = new ValidInput(question, history!, images);
        return true;
    }

    // Base64 is decoded here; ChatRules then checks count, size and that the bytes are the declared format.
    private static ChatImage[] DecodeImages(IReadOnlyList<ChatImageDto?>? images, out string? error)
    {
        error = null;
        if (images is null || images.Count == 0)
        {
            return [];
        }

        if (images.Count > ChatRules.MaxImagesPerQuestion)
        {
            error = $"Too many images ({images.Count}; the maximum is {ChatRules.MaxImagesPerQuestion} per question).";
            return [];
        }

        var decoded = new ChatImage[images.Count];
        for (var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            if (string.IsNullOrWhiteSpace(image?.MediaType) || string.IsNullOrEmpty(image.Data))
            {
                error = $"Image {i + 1} needs a mediaType and base64 data.";
                return [];
            }

            // Rejected by length before decoding (base64 grows 4/3), so an oversized payload never allocates its buffer.
            if ((long)image.Data.Length * 3 / 4 > ChatRules.MaxImageBytes + 2)
            {
                error = $"Image {i + 1} is too large (the maximum is {ChatRules.MaxImageBytes / 1024 / 1024} MB).";
                return [];
            }

            var buffer = new byte[image.Data.Length * 3 / 4 + 3];
            if (!Convert.TryFromBase64String(image.Data, buffer, out var length))
            {
                error = $"Image {i + 1} is not valid base64.";
                return [];
            }

            decoded[i] = new ChatImage(image.MediaType, buffer.AsMemory(0, length));
        }

        return decoded;
    }
}
