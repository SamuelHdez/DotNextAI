using System.ComponentModel.DataAnnotations;

namespace DotNextAI.Api.RateLimiting;

/// <summary>
/// Limits of the endpoints that spend the LiteLLM key. The API has no authentication (local use), so a fixed window
/// per client IP keeps a runaway script or an exposed port from burning the key's budget.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public const string ChatPolicy = "chat";

    /// <summary>Requests to <c>/api/chat</c> allowed per client IP and minute.</summary>
    [Range(1, 10_000)]
    public int ChatPermitsPerMinute { get; init; } = 20;
}
