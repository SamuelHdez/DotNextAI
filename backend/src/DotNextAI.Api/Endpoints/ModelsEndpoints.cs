using DotNextAI.Api.Contracts;
using DotNextAI.Application.Chat;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNextAI.Api.Endpoints;

/// <summary>Backlog step 08: the chat models a client may choose from (docs/design/api.md#get-apimodels).</summary>
internal static class ModelsEndpoints
{
    public static IEndpointRouteBuilder MapModelsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/models", GetModelsAsync)
            .WithName("GetModels")
            .WithTags("Chat")
            .WithSummary("Chat models of the LiteLLM proxy (mode = chat), the default first.")
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    private static async Task<Results<Ok<ModelsResponseDto>, ProblemHttpResult>> GetModelsAsync(
        IModelCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (catalog.DefaultModel is not { } defaultModel)
        {
            return ChatProblems.NotConfigured();
        }

        try
        {
            var models = await catalog.GetChatModelsAsync(cancellationToken);
            return TypedResults.Ok(new ModelsResponseDto(
                defaultModel,
                models.Select(model => new ChatModelDto(model.Id, model.IsDefault, model.MaxInputTokens, model.MaxOutputTokens)).ToArray()));
        }
        catch (ChatServiceException ex)
        {
            return ChatProblems.FromException(ex);
        }
    }
}
