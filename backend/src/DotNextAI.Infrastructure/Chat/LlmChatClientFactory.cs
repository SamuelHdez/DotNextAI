using System.ClientModel;
using System.ClientModel.Primitives;
using DotNextAI.Infrastructure.Options;
using OpenAI;
using OpenAI.Chat;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Builds the OpenAI SDK <see cref="ChatClient"/> pointed at the LiteLLM proxy. Every model call (direct or through an
/// Agent Framework agent) goes through a client made here, so endpoint, key, timeout and retries are set in one place.
/// </summary>
internal static class LlmChatClientFactory
{
    /// <summary>Named <see cref="HttpClient"/> used as the SDK's transport (stubbed in tests).</summary>
    public const string HttpClientName = "Llm";

    public static ChatClient Create(LlmOptions options, string model, IHttpClientFactory httpClientFactory) =>
        new(
            model,
            new ApiKeyCredential(options.ApiKey!),
            new OpenAIClientOptions
            {
                // Used as is: the SDK appends "chat/completions". LiteLLM serves it with and without "/v1".
                Endpoint = new Uri(options.Endpoint!),
                NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                RetryPolicy = new ClientRetryPolicy(options.MaxRetries),
                Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(HttpClientName)),
                UserAgentApplicationId = "DotNextAI",
            });
}
