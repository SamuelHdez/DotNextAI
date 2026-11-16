using System.ClientModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Backlog step 02: the question goes through a Microsoft Agent Framework agent (<see cref="ChatClientAgent"/>) built on
/// the OpenAI SDK client of the LiteLLM proxy. Step 01's direct call lives on in git history (commit 2fcbc2a).
/// Step 03: the caller's history is sent before the question; the agent keeps no state between calls.
/// Step 06: images go with the question as <see cref="DataContent"/>; the SDK sends them as base64 data URIs.
/// Step 07: <see cref="AskStreamingAsync"/> relays the agent's streamed updates as they arrive.
/// Step 08: one agent per chat model, created on first use; a model must be in the <see cref="IModelCatalog"/>.
/// </summary>
internal sealed partial class AgentChatService : IChatService
{
    public const string AgentName = "DotNextAI";

    /// <summary>System instructions sent with every run (docs/specs/feature-llm-chat.md#agent).</summary>
    public const string Instructions =
        "You are DotNextAI, a helpful assistant. Answer clearly and concisely, in the language of the question.";

    private readonly LlmOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IModelCatalog _catalog;
    private readonly ConcurrentDictionary<string, AIAgent> _agents = new(StringComparer.Ordinal);
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        IOptions<LlmOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IModelCatalog catalog)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _catalog = catalog;
        _logger = loggerFactory.CreateLogger<AgentChatService>();
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<ChatAnswer> AskAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var (prompt, model) = Validate(request);
        var agent = await AgentForAsync(model, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        AgentResponse response;
        try
        {
            // No AgentSession: the front end owns the history (CLI process, browser), so the API can stay stateless.
            response = await agent.RunAsync(ToMessages(request.History, prompt, request.Images), cancellationToken: cancellationToken);
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

    public async IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (prompt, model) = Validate(request);
        var agent = await AgentForAsync(model, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        var text = new StringBuilder();
        Microsoft.Extensions.AI.ChatFinishReason? finishReason = null;

        var updates = agent.RunStreamingAsync(ToMessages(request.History, prompt, request.Images), cancellationToken: cancellationToken);
        await using var enumerator = updates.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            // C# cannot yield inside a try with a catch, so only the move to the next update is guarded.
            AgentResponseUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                update = enumerator.Current;
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
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                LogCallFailed(model, 0);
                throw text.Length > 0 ? ChatFailureMapper.Interrupted(ex) : ChatFailureMapper.Unreachable(ex);
            }

            finishReason = update.FinishReason ?? finishReason;
            if ((update.RawRepresentation as ChatResponseUpdate)?.ModelId is { Length: > 0 } answeredBy)
            {
                model = answeredBy;
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                text.Append(update.Text);
                yield return new ChatStreamUpdate(update.Text);
            }
        }

        if (string.IsNullOrWhiteSpace(text.ToString()))
        {
            throw new ChatServiceException(ChatFailure.EmptyAnswer, "The model returned an empty answer.");
        }

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogCallCompleted(model, finishReason?.Value, elapsedMs);
        yield return new ChatStreamUpdate(
            string.Empty,
            new ChatAnswer(text.ToString(), model, Truncated: finishReason == Microsoft.Extensions.AI.ChatFinishReason.Length));
    }

    /// <summary>The rules of every call; returns the trimmed question and the model to use.</summary>
    private (string Prompt, string Model) Validate(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prompt = ChatRules.NormalizeQuestion(request.Question);
        ChatRules.ValidateHistory(request.History);
        ChatRules.ValidateImages(request.Images);
        if (!_options.IsConfigured)
        {
            throw ChatFailureMapper.NotConfigured();
        }

        return (prompt, string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel! : request.Model.Trim());
    }

    /// <summary>
    /// The agent of a chat model, created on first use. Any model other than the default must be a chat model of the
    /// proxy (<see cref="IModelCatalog"/>): callers cannot send arbitrary model names to the proxy.
    /// </summary>
    private async Task<AIAgent> AgentForAsync(string model, CancellationToken cancellationToken)
    {
        if (!string.Equals(model, _options.DefaultModel, StringComparison.Ordinal))
        {
            var models = await _catalog.GetChatModelsAsync(cancellationToken);
            if (!models.Any(chatModel => string.Equals(chatModel.Id, model, StringComparison.Ordinal)))
            {
                throw ChatFailureMapper.NotAChatModel(model);
            }
        }

        return _agents.GetOrAdd(model, id => LlmChatClientFactory
            .Create(_options, id, _httpClientFactory)
            .AsAIAgent(instructions: Instructions, name: AgentName, loggerFactory: _loggerFactory));
    }

    private static List<ChatMessage> ToMessages(IReadOnlyList<ChatTurn> history, string question, IReadOnlyList<ChatImage> images)
    {
        var messages = new List<ChatMessage>(history.Count + 1);
        messages.AddRange(history.Select(turn => new ChatMessage(
            turn.Role == ChatTurnRole.User ? ChatRole.User : ChatRole.Assistant,
            turn.Text)));

        List<AIContent> contents = [new TextContent(question)];
        contents.AddRange(images.Select(image => new DataContent(image.Data, image.MediaType)));
        messages.Add(new ChatMessage(ChatRole.User, contents));
        return messages;
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
