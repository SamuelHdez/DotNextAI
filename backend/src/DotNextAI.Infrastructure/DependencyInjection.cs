using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNextAI.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the LLM options and the chat service built on them.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Missing values are allowed (the API starts and reports LlmConfigured = false); invalid ranges are not.
        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The SDK enforces Llm:TimeoutSeconds per attempt (NetworkTimeout); HttpClient's own 100 s limit would cut
        // longer answers first.
        services.AddHttpClient(OpenAiChatService.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<IChatService, OpenAiChatService>();

        return services;
    }
}
