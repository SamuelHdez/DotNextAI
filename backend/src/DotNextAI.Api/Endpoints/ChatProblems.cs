using DotNextAI.Application.Chat;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNextAI.Api.Endpoints;

/// <summary>
/// HTTP answers for model failures (docs/design/api.md#errors). Every problem carries a <c>failure</c> extension with
/// the <see cref="ChatFailure"/> name, so the web app shows its own localized text instead of the English detail.
/// </summary>
internal static class ChatProblems
{
    public const string FailureExtension = "failure";

    public static int StatusCode(ChatFailure failure) => failure switch
    {
        ChatFailure.NotConfigured or ChatFailure.ModelUnavailable => StatusCodes.Status503ServiceUnavailable,
        ChatFailure.RateLimited => StatusCodes.Status429TooManyRequests,
        ChatFailure.Timeout => StatusCodes.Status504GatewayTimeout,
        ChatFailure.BadRequest => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status502BadGateway,
    };

    public static ProblemHttpResult FromException(ChatServiceException exception) =>
        Create(exception.Failure, exception.Message);

    public static ProblemHttpResult NotConfigured() =>
        Create(ChatFailure.NotConfigured, "The LiteLLM proxy is not configured on the server.");

    public static ProblemHttpResult TooManyRequests() =>
        Create(ChatFailure.RateLimited, "Too many questions in a short time. Wait a minute and try again.");

    private static ProblemHttpResult Create(ChatFailure failure, string detail) =>
        TypedResults.Problem(
            detail: detail,
            statusCode: StatusCode(failure),
            title: Title(failure),
            extensions: new Dictionary<string, object?> { [FailureExtension] = failure.ToString() });

    public static string Title(ChatFailure failure) => failure switch
    {
        ChatFailure.NotConfigured => "The model is not configured",
        ChatFailure.Unauthorized => "The model provider rejected the server's key",
        ChatFailure.ModelNotFound => "Unknown model",
        ChatFailure.ModelUnavailable => "The model is unavailable",
        ChatFailure.BadRequest => "The model provider could not process the request",
        ChatFailure.RateLimited => "Too many requests",
        ChatFailure.Timeout => "The model did not answer in time",
        ChatFailure.EmptyAnswer => "The model returned an empty answer",
        _ => "The model provider is unavailable",
    };
}
