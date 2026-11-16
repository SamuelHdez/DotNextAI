using DotNextAI.Application.Health;
using DotNextAI.Infrastructure.Options;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace DotNextAI.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", GetHealth)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Liveness of the API and whether the LLM proxy is configured.");

        return app;
    }

    private static Ok<HealthStatusDto> GetHealth(
        IHostEnvironment environment,
        IOptions<LlmOptions> llm,
        TimeProvider timeProvider) =>
        TypedResults.Ok(new HealthStatusDto(
            Status: "Healthy",
            Service: "DotNextAI.Api",
            Environment: environment.EnvironmentName,
            LlmConfigured: llm.Value.IsConfigured,
            TimestampUtc: timeProvider.GetUtcNow()));
}
