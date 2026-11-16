namespace DotNextAI.Application.Chat;

/// <summary>Why a model call failed. Front ends map each value to their own message or status code.</summary>
public enum ChatFailure
{
    /// <summary>The proxy could not be reached or answered with a server error.</summary>
    Unavailable = 0,

    /// <summary>Endpoint, API key or default model are missing.</summary>
    NotConfigured,

    /// <summary>The proxy rejected the API key (401/403).</summary>
    Unauthorized,

    /// <summary>The proxy does not know the requested model (404).</summary>
    ModelNotFound,

    /// <summary>The proxy knows the model but has no healthy deployment for it right now (LiteLLM answers 400).</summary>
    ModelUnavailable,

    /// <summary>The proxy rejected the request as invalid (400), e.g. a context window exceeded.</summary>
    BadRequest,

    /// <summary>Rate limit or budget of the key reached (429), after the SDK's retries.</summary>
    RateLimited,

    /// <summary>No answer within <c>Llm:TimeoutSeconds</c>.</summary>
    Timeout,

    /// <summary>The model answered with no text.</summary>
    EmptyAnswer,
}
