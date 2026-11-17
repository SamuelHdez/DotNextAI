using DotNextAI.Api.Contracts;
using DotNextAI.Api.RateLimiting;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Conversations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNextAI.Api.Endpoints;

/// <summary>Saved chats and their folders (docs/design/api.md#saved-chats, docs/specs/feature-saved-chats.md).</summary>
internal static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var folders = app.MapGroup("/api/folders").WithTags("Saved chats");
        folders.MapGet("/", ListFoldersAsync).WithName("ListFolders");
        folders.MapPost("/", CreateFolderAsync).WithName("CreateFolder").ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        folders.MapPatch("/{id:guid}", UpdateFolderAsync).WithName("UpdateFolder")
            .WithDescription("Renames the folder and/or changes its icon and colour; omitted fields stay as they are.").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        folders.MapDelete("/{id:guid}", DeleteFolderAsync).WithName("DeleteFolder")
            .WithDescription("Deletes the folder; its conversations stay, without a folder.").ProducesProblem(StatusCodes.Status404NotFound);

        var conversations = app.MapGroup("/api/conversations").WithTags("Saved chats");
        conversations.MapGet("/", ListConversationsAsync).WithName("ListConversations").WithDescription("Every saved conversation, the most recently updated first.");
        conversations.MapPost("/", CreateConversationAsync).WithName("CreateConversation").ProducesValidationProblem();
        conversations.MapGet("/{id:guid}", GetConversationAsync).WithName("GetConversation").ProducesProblem(StatusCodes.Status404NotFound);
        conversations.MapPatch("/{id:guid}", RenameConversationAsync).WithName("RenameConversation").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        conversations.MapPut("/{id:guid}/folder", MoveConversationAsync).WithName("MoveConversation").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        conversations.MapDelete("/{id:guid}", DeleteConversationAsync).WithName("DeleteConversation").ProducesProblem(StatusCodes.Status404NotFound);
        conversations.MapGet("/{id:guid}/images/{imageId:guid}", GetImageAsync).WithName("GetConversationImage").ProducesProblem(StatusCodes.Status404NotFound);
        conversations.MapPost("/{id:guid}/messages/stream", AskAsync)
            .WithName("AskInConversation")
            .WithSummary("Ask in a saved conversation: the history comes from the database, the exchange is saved.")
            .WithDescription("Same events as POST /api/chat/stream; `done` also has `conversation`. A stopped answer is saved with the text so far.")
            .Accepts<ConversationMessageDto>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimitingOptions.ChatPolicy);

        return app;
    }

    public static ConversationSummaryDto ToDto(ConversationSummary summary) =>
        new(
            summary.Id,
            summary.Title,
            summary.FolderId,
            summary.Model,
            summary.QuestionCount,
            TokenUsageDto.From(summary.TotalUsage)!,
            summary.CreatedAt,
            summary.UpdatedAt);

    private static FolderDto ToDto(FolderInfo folder) => new(folder.Id, folder.Name, folder.Icon, folder.Color, folder.ConversationCount, folder.CreatedAt);

    /// <summary>The icon and colour of a folder request: each must be a key of <see cref="FolderAppearance"/> when given.</summary>
    private static Dictionary<string, string[]> AppearanceErrors(FolderRequestDto request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Icon is not null && !FolderAppearance.IsIcon(request.Icon))
        {
            errors["icon"] = [$"Unknown icon. Use one of: {string.Join(", ", FolderAppearance.Icons)}."];
        }

        if (request.Color is not null && !FolderAppearance.IsColor(request.Color))
        {
            errors["color"] = [$"Unknown colour. Use one of: {string.Join(", ", FolderAppearance.Colors)}."];
        }

        return errors;
    }

    private static async Task<Ok<FolderDto[]>> ListFoldersAsync(IConversationStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok((await store.ListFoldersAsync(cancellationToken)).Select(ToDto).ToArray());

    private static async Task<Results<Created<FolderDto>, ValidationProblem, ProblemHttpResult>> CreateFolderAsync(
        FolderRequestDto request,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        var errors = AppearanceErrors(request);
        if (!ConversationRules.TryNormalizeFolderName(request.Name, out var name, out var error))
        {
            errors["name"] = [error];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var folder = await store.CreateFolderAsync(
                name,
                request.Icon ?? FolderAppearance.DefaultIcon,
                request.Color ?? FolderAppearance.DefaultColor,
                cancellationToken);
            return TypedResults.Created($"/api/folders/{folder.Id}", ToDto(folder));
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
    }

    private static async Task<Results<Ok<FolderDto>, ValidationProblem, ProblemHttpResult>> UpdateFolderAsync(
        Guid id,
        FolderRequestDto request,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        var errors = AppearanceErrors(request);
        string? name = null;
        if (request.Name is not null)
        {
            if (ConversationRules.TryNormalizeFolderName(request.Name, out var normalized, out var error))
            {
                name = normalized;
            }
            else
            {
                errors["name"] = [error];
            }
        }
        else if (request.Icon is null && request.Color is null)
        {
            errors["name"] = ["Give a name, an icon or a colour to change."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            return TypedResults.Ok(ToDto(await store.UpdateFolderAsync(id, new FolderUpdate(name, request.Icon, request.Color), cancellationToken)));
        }
        catch (ConversationStoreException ex)
        {
            return FolderProblem(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteFolderAsync(Guid id, IConversationStore store, CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteFolderAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (ConversationStoreException ex)
        {
            return FolderProblem(ex);
        }
    }

    private static async Task<Ok<ConversationSummaryDto[]>> ListConversationsAsync(IConversationStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok((await store.ListConversationsAsync(cancellationToken)).Select(ToDto).ToArray());

    private static async Task<Results<Created<ConversationSummaryDto>, ValidationProblem, ProblemHttpResult>> CreateConversationAsync(
        CreateConversationDto? request,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        var title = ConversationRules.UntitledTitle;
        if (request?.Title is { } requested && !ConversationRules.TryNormalizeTitle(requested, out title, out var error))
        {
            return Invalid("title", error);
        }

        try
        {
            var conversation = await store.CreateConversationAsync(title, request?.FolderId, cancellationToken);
            return TypedResults.Created($"/api/conversations/{conversation.Id}", ToDto(conversation));
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
    }

    private static async Task<Results<Ok<ConversationDetailDto>, ProblemHttpResult>> GetConversationAsync(
        Guid id,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        if (await store.GetConversationAsync(id, cancellationToken) is not { } conversation)
        {
            return Problem(new ConversationStoreException(StoreFailure.NotFound, "The conversation does not exist."));
        }

        return TypedResults.Ok(new ConversationDetailDto(
            ToDto(conversation.Summary),
            conversation.Messages.Select(message => new StoredMessageDto(
                message.Id,
                message.Role,
                message.Text,
                message.Images.Select(image => new StoredImageDto(image.Id, image.MediaType, image.Size, $"/api/conversations/{id}/images/{image.Id}")).ToArray(),
                message.Model,
                TokenUsageDto.From(message.Usage),
                message.Truncated,
                message.Stopped,
                message.CreatedAt)).ToArray()));
    }

    private static async Task<Results<Ok<ConversationSummaryDto>, ValidationProblem, ProblemHttpResult>> RenameConversationAsync(
        Guid id,
        RenameConversationDto request,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        if (!ConversationRules.TryNormalizeTitle(request.Title, out var title, out var error))
        {
            return Invalid("title", error);
        }

        try
        {
            return TypedResults.Ok(ToDto(await store.RenameConversationAsync(id, title, cancellationToken)));
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
    }

    private static async Task<Results<Ok<ConversationSummaryDto>, ValidationProblem, ProblemHttpResult>> MoveConversationAsync(
        Guid id,
        MoveConversationDto request,
        IConversationStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(ToDto(await store.MoveConversationAsync(id, request.FolderId, cancellationToken)));
        }
        catch (ConversationStoreException ex) when (ex.Failure == StoreFailure.FolderNotFound)
        {
            return Invalid("folderId", ex.Message);
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteConversationAsync(Guid id, IConversationStore store, CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteConversationAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetImageAsync(
        Guid id,
        Guid imageId,
        IConversationStore store,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (await store.GetImageAsync(id, imageId, cancellationToken) is not { } image)
        {
            return Problem(new ConversationStoreException(StoreFailure.NotFound, "The image does not exist."));
        }

        // A saved image never changes: the browser may keep it.
        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.File(image.Data.ToArray(), image.MediaType);
    }

    /// <summary>The same validation and first-piece rule as POST /api/chat/stream, with the history from the database.</summary>
    private static async Task<IResult> AskAsync(
        Guid id,
        ConversationMessageDto request,
        ConversationService conversations,
        IChatService chat,
        IModelCatalog catalog,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ChatRules.TryNormalizeQuestion(request.Question, out var question, out var questionError))
        {
            errors["question"] = [questionError];
        }

        var images = ChatEndpoints.DecodeImages(request.Images, out var imagesError);
        if (imagesError is null && !ChatRules.TryValidateImages(images, out var rulesError))
        {
            imagesError = rulesError;
        }

        if (imagesError is not null)
        {
            errors["images"] = [imagesError];
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!chat.IsConfigured)
        {
            return ChatProblems.NotConfigured();
        }

        IAsyncEnumerator<ConversationStreamUpdate>? updates = null;
        try
        {
            if (await ChatEndpoints.UnknownModelAsync(model, catalog, cancellationToken) is { } unknown)
            {
                return unknown;
            }

            updates = conversations.AskStreamingAsync(id, question, images, model, cancellationToken).GetAsyncEnumerator(cancellationToken);
            var hasFirst = await updates.MoveNextAsync();
            var stream = updates;
            updates = null;
            return TypedResults.ServerSentEvents(ChatStreamEvents.FromConversation(stream, hasFirst, cancellationToken));
        }
        catch (ConversationStoreException ex)
        {
            return Problem(ex);
        }
        catch (ChatServiceException ex)
        {
            return ChatProblems.FromException(ex);
        }
        finally
        {
            if (updates is not null)
            {
                await updates.DisposeAsync();
            }
        }
    }

    private static ValidationProblem Invalid(string field, string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [error] });

    /// <summary>On /api/folders/{id} the folder is the resource: a missing one is 404, not a bad reference.</summary>
    private static ProblemHttpResult FolderProblem(ConversationStoreException exception) =>
        exception.Failure == StoreFailure.FolderNotFound
            ? Problem(new ConversationStoreException(StoreFailure.NotFound, exception.Message))
            : Problem(exception);

    private static ProblemHttpResult Problem(ConversationStoreException exception) =>
        TypedResults.Problem(
            detail: exception.Message,
            statusCode: exception.Failure switch
            {
                StoreFailure.DuplicateFolderName or StoreFailure.ConversationFull => StatusCodes.Status409Conflict,
                StoreFailure.FolderNotFound => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status404NotFound,
            },
            title: exception.Failure switch
            {
                StoreFailure.DuplicateFolderName => "The folder name is taken",
                StoreFailure.ConversationFull => "The conversation is full",
                StoreFailure.FolderNotFound => "Unknown folder",
                _ => "Not found",
            },
            extensions: new Dictionary<string, object?> { ["failure"] = exception.Failure.ToString() });
}
