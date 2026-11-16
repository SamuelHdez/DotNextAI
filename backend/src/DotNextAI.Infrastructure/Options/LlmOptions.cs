using System.ComponentModel.DataAnnotations;

namespace DotNextAI.Infrastructure.Options;

/// <summary>
/// ERNI LiteLLM proxy (OpenAI-compatible API) used for every model call. Calls are disabled until
/// <see cref="Endpoint"/>, <see cref="ApiKey"/> and <see cref="DefaultModel"/> are set. The key is a secret: it lives in
/// <c>dotnet user-secrets</c> (set it with <c>./scripts/dev.sh secrets</c>) and never leaves the backend.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Base URL of the proxy, e.g. <c>https://litellm.example.com/</c>. HTTPS, or HTTP only on loopback.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Virtual key issued by the proxy.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Model name as exposed by the proxy (its <c>/v1/models</c> list).</summary>
    public string? DefaultModel { get; init; }

    /// <summary>Maximum time for one model call (each attempt), in seconds.</summary>
    [Range(5, 600)]
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Retries of the OpenAI SDK on 408, 429 and 5xx answers and on network errors, with exponential back-off.</summary>
    [Range(0, 5)]
    public int MaxRetries { get; init; } = 2;

    public bool IsConfigured =>
        IsAllowedEndpoint(Endpoint)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(DefaultModel);

    private static bool IsAllowedEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}
