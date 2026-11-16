using System.ClientModel;
using System.Diagnostics;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Backlog step 02: the question goes through a Microsoft Agent Framework agent (<see cref="ChatClientAgent"/>) built on
/// the OpenAI SDK client of the LiteLLM proxy. Step 01's direct call lives on in git history (commit 2fcbc2a); the
/// contract and the HTTP request stay the same, plus the agent's instructions.
/// </summary>
internal sealed partial class AgentChatService : IChatService
{
    public const string AgentName = "DotNextAI";

    /// <summary>System instructions sent with every run (docs/specs/feature-llm-chat.md#agent).</summary>
    public const string Instructions =
        "You are DotNextAI, a helpful assistant. Answer clearly and concisely, in the language of the question.";

    private readonly LlmOptions _options;
    private readonly AIAgent? _agent;
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        IOptions<LlmOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<AgentChatService>();

        if (_options.IsConfigured)
        {
            _agent = LlmChatClientFactory
                .Create(_options, _options.DefaultModel!, httpClientFactory)
                .AsAIAgent(instructions: Instructions, name: AgentName, loggerFactory: loggerFactory);
        }
    }

    public bool IsConfigured => _agent is not null;

    public async Task<ChatAnswer> AskAsync(string question, CancellationToken cancellationToken)
    {
        var prompt = ChatRules.NormalizeQuestion(question);
        if (_agent is null)
        {
            throw ChatFailureMapper.NotConfigured();
        }

        var model = _options.DefaultModel!;
        var started = Stopwatch.GetTimestamp();
        AgentResponse response;
        try
        {
            // No session: every question is independent until chat history arrives (step 03).
            response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        }
        catch (ClientResultException ex)
        {
            LogCallFailed(model, ex.Status);
            throw ChatFailureMapper.FromStatus(ex, _options, model);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogCallFailed(model, 0);
            throw ChatFailureMapper.Timeout(_options, ex);
        }
        catch (HttpRequestException ex)
        {
            LogCallFailed(model, 0);
            throw ChatFailureMapper.Unreachable(ex);
        }

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            // A refusal comes without text content: show the model's own explanation.
            text = Refusal(response) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatServiceException(ChatFailure.EmptyAnswer, "The model returned an empty answer.");
        }

        var answeredBy = (response.RawRepresentation as ChatResponse)?.ModelId;
        if (!string.IsNullOrWhiteSpace(answeredBy))
        {
            model = answeredBy;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogCallCompleted(model, response.FinishReason?.Value, elapsedMs);

        return new ChatAnswer(text, model, Truncated: response.FinishReason == Microsoft.Extensions.AI.ChatFinishReason.Length);
    }

    // The agent keeps the provider's object: ChatResponse → OpenAI ChatCompletion, which carries the refusal.
    private static string? Refusal(AgentResponse response) =>
        ((response.RawRepresentation as ChatResponse)?.RawRepresentation as ChatCompletion)?.Refusal;

    // Only metadata: prompts and answers are never logged (docs/specs/product-rules.md).
    [LoggerMessage(Level = LogLevel.Information, Message = "Agent run with {Model} finished ({FinishReason}) in {ElapsedMs:F0} ms")]
    private partial void LogCallCompleted(string model, string? finishReason, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent run with {Model} failed (HTTP status {Status})")]
    private partial void LogCallFailed(string model, int status);
}
