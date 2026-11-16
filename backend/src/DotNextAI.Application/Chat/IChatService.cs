namespace DotNextAI.Application.Chat;

/// <summary>
/// Sends questions to the language model through the configured LiteLLM proxy. Used by the CLI and, from backlog step
/// 04, by the API, so both front ends share one implementation.
/// </summary>
public interface IChatService
{
    /// <summary>Whether the proxy endpoint, API key and default model are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Asks a single question (no conversation history) to the default model.</summary>
    /// <param name="question">Non-empty question of at most <see cref="ChatRules.MaxQuestionLength"/> characters.</param>
    /// <param name="cancellationToken">Cancels the call (Ctrl+C in the CLI, an aborted request in the API).</param>
    /// <exception cref="ArgumentException">The question is empty or too long.</exception>
    /// <exception cref="ChatServiceException">The model could not answer; <see cref="ChatServiceException.Failure"/> says why.</exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
    Task<ChatAnswer> AskAsync(string question, CancellationToken cancellationToken);
}
