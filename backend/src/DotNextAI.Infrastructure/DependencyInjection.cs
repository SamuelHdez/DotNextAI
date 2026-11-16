using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNextAI.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the LLM options and, in later steps, the chat and image clients built on them.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Missing values are allowed (the API starts and reports LlmConfigured = false); invalid ranges are not.
        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
