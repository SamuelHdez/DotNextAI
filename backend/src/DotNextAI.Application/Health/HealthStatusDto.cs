namespace DotNextAI.Application.Health;

/// <summary>Liveness answer of the API.</summary>
/// <param name="Status">Always <c>Healthy</c> when the API answers.</param>
/// <param name="Service">Name of the service that answered.</param>
/// <param name="Environment">ASP.NET Core environment name.</param>
/// <param name="LlmConfigured">Whether the LiteLLM proxy endpoint, API key and default model are set.</param>
/// <param name="TimestampUtc">Server time of the answer.</param>
public sealed record HealthStatusDto(
    string Status,
    string Service,
    string Environment,
    bool LlmConfigured,
    DateTimeOffset TimestampUtc);
