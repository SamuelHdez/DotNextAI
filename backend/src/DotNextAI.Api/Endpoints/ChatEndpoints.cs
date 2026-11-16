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

        return app;
    }

    private static async Task<Results<Ok<ChatResponseDto>, ValidationProblem, ProblemHttpResult>> AskAsync(
        ChatRequestDto request,
        IChatService chat,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
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

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!chat.IsConfigured)
        {
            return ChatProblems.NotConfigured();
        }

        try
        {
            var answer = await chat.AskAsync(question, history!, cancellationToken);
            return TypedResults.Ok(new ChatResponseDto(answer.Text, answer.Model, answer.Truncated));
        }
        catch (ChatServiceException ex)
        {
            return ChatProblems.FromException(ex);
        }
    }
}
