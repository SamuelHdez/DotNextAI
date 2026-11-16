using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Backlog step 01: a direct chat completion with the OpenAI .NET SDK against the LiteLLM proxy (OpenAI-compatible),
/// with no orchestration layer. The HTTP transport comes from <see cref="IHttpClientFactory"/> so tests can stub it.
/// </summary>
internal sealed partial class OpenAiChatService : IChatService
{
    public const string HttpClientName = "Llm";

    private readonly LlmOptions _options;
    private readonly ChatClient? _client;
    private readonly ILogger<OpenAiChatService> _logger;

    public OpenAiChatService(
        IOptions<LlmOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiChatService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsConfigured)
        {
            _client = new ChatClient(
                _options.DefaultModel,
                new ApiKeyCredential(_options.ApiKey!),
                new OpenAIClientOptions
                {
                    // Used as is: the SDK appends "chat/completions". LiteLLM serves it with and without "/v1".
                    Endpoint = new Uri(_options.Endpoint!),
                    NetworkTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
                    RetryPolicy = new ClientRetryPolicy(_options.MaxRetries),
                    Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(HttpClientName)),
                    UserAgentApplicationId = "DotNextAI",
                });
        }
    }

    public bool IsConfigured => _client is not null;

    public async Task<ChatAnswer> AskAsync(string question, CancellationToken cancellationToken)
    {
        var prompt = ChatRules.NormalizeQuestion(question);
        if (_client is null)
        {
            throw new ChatServiceException(
                ChatFailure.NotConfigured,
                "The LiteLLM proxy is not configured (Llm:Endpoint, Llm:ApiKey and Llm:DefaultModel).");
        }

        var started = Stopwatch.GetTimestamp();
        ChatCompletion completion;
        try
        {
            completion = (await _client.CompleteChatAsync([new UserChatMessage(prompt)], cancellationToken: cancellationToken)).Value;
        }
        catch (ClientResultException ex)
        {
            LogCallFailed(_options.DefaultModel!, ex.Status);
            throw MapFailure(ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogCallFailed(_options.DefaultModel!, 0);
            throw new ChatServiceException(
                ChatFailure.Timeout,
                $"The model did not answer within {_options.TimeoutSeconds} seconds.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            LogCallFailed(_options.DefaultModel!, 0);
            throw new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached.", ex);
        }

        var text = string.Concat(completion.Content
            .Where(part => part.Kind == ChatMessageContentPartKind.Text)
            .Select(part => part.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            // A refusal comes without content: show the model's own explanation.
            text = completion.Refusal ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatServiceException(ChatFailure.EmptyAnswer, "The model returned an empty answer.");
        }

        var model = string.IsNullOrWhiteSpace(completion.Model) ? _options.DefaultModel! : completion.Model;
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogCallCompleted(model, completion.FinishReason, elapsedMs);

        return new ChatAnswer(text, model, Truncated: completion.FinishReason == ChatFinishReason.Length);
    }

    private ChatServiceException MapFailure(ClientResultException ex)
    {
        var model = _options.DefaultModel;
        switch (ex.Status)
        {
            // Never quote the body here: LiteLLM may echo (part of) the key it rejected.
            case 401 or 403:
                return new ChatServiceException(ChatFailure.Unauthorized, "The LiteLLM proxy rejected the API key.", ex);
            case 429:
                return new ChatServiceException(
                    ChatFailure.RateLimited,
                    "The rate limit or budget of the API key was reached. Try again later.",
                    ex);
            case 0:
                return new ChatServiceException(ChatFailure.Unavailable, "The LiteLLM proxy could not be reached.", ex);
        }

        var detail = ProxyErrorDetail.From(ex, _options.ApiKey);
        if (detail?.Contains("no healthy deployments", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ChatServiceException(
                ChatFailure.ModelUnavailable,
                $"The model '{model}' is currently unavailable in the LiteLLM proxy (no healthy deployments).",
                ex);
        }

        var suffix = detail is null ? "." : $": {detail}";
        return ex.Status switch
        {
            404 => new ChatServiceException(ChatFailure.ModelNotFound, $"The LiteLLM proxy does not know the model '{model}'{suffix}", ex),
            400 or 413 or 422 => new ChatServiceException(ChatFailure.BadRequest, $"The LiteLLM proxy rejected the request{suffix}", ex),
            _ => new ChatServiceException(ChatFailure.Unavailable, $"The LiteLLM proxy answered with HTTP {ex.Status}.", ex),
        };
    }

    // Only metadata: prompts and answers are never logged (docs/specs/product-rules.md).
    [LoggerMessage(Level = LogLevel.Information, Message = "Chat completion with {Model} finished ({FinishReason}) in {ElapsedMs:F0} ms")]
    private partial void LogCallCompleted(string model, ChatFinishReason? finishReason, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chat completion with {Model} failed (HTTP status {Status})")]
    private partial void LogCallFailed(string model, int status);
}
