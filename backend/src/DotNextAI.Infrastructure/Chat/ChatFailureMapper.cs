using System.ClientModel;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Turns the exceptions of a model call into <see cref="ChatServiceException"/> with a <see cref="ChatFailure"/> and a
/// message safe to show to the caller (docs/specs/feature-llm-chat.md#failures).
/// </summary>
internal static class ChatFailureMapper
{
    public static ChatServiceException NotConfigured() =>
        new(
            ChatFailure.NotConfigured,
            "The LiteLLM proxy is not configured (Llm:Endpoint, Llm:ApiKey and Llm:DefaultModel).");

    public static ChatServiceException Timeout(LlmOptions options, Exception exception) =>
        new(ChatFailure.Timeout, $"The model did not answer within {options.TimeoutSeconds} seconds.", exception);

    public static ChatServiceException Unreachable(Exception exception) =>
        new(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached.", exception);

    /// <summary>A streamed answer whose connection broke after some text had arrived (step 07).</summary>
    public static ChatServiceException Interrupted(Exception exception) =>
        new(ChatFailure.Unavailable, "The connection to the LiteLLM proxy broke before the answer was complete.", exception);

    public static ChatServiceException FromStatus(ClientResultException exception, LlmOptions options, string model)
    {
        switch (exception.Status)
        {
            // Never quote the body here: LiteLLM may echo (part of) the key it rejected.
            case 401 or 403:
                return new ChatServiceException(ChatFailure.Unauthorized, "The LiteLLM proxy rejected the API key.", exception);
            case 429:
                return new ChatServiceException(
                    ChatFailure.RateLimited,
                    "The rate limit or budget of the API key was reached. Try again later.",
                    exception);
            case 0:
                return Unreachable(exception);
        }

        var detail = ProxyErrorDetail.From(exception, options.ApiKey);
        if (detail?.Contains("no healthy deployments", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ChatServiceException(
                ChatFailure.ModelUnavailable,
                $"The model '{model}' is currently unavailable in the LiteLLM proxy (no healthy deployments).",
                exception);
        }

        var suffix = detail is null ? "." : $": {detail}";
        return exception.Status switch
        {
            404 => new ChatServiceException(ChatFailure.ModelNotFound, $"The LiteLLM proxy does not know the model '{model}'{suffix}", exception),
            400 or 413 or 422 => new ChatServiceException(ChatFailure.BadRequest, $"The LiteLLM proxy rejected the request{suffix}", exception),
            _ => new ChatServiceException(ChatFailure.Unavailable, $"The LiteLLM proxy answered with HTTP {exception.Status}.", exception),
        };
    }
}
