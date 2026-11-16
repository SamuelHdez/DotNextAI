using System.Threading.RateLimiting;
using DotNextAI.Api.Endpoints;
using Microsoft.AspNetCore.RateLimiting;

namespace DotNextAI.Api.RateLimiting;

internal static class RateLimitingExtensions
{
    public static IServiceCollection AddDotNextAIRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(RateLimitingOptions.ChatPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.ChatPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await ChatProblems.TooManyRequests().ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }
}
